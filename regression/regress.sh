#!/usr/bin/env bash
# Usage: regress.sh <config.env>. See README.md.
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
config=${1:?usage: regress.sh <config.env>}
# shellcheck disable=SC1090
source "$config"

: "${PROJECT_PREFIX:?}" "${SAMPLE:?}" "${CASES:?}" "${THREADS:=1}" "${BATCH:=1000}"
: "${LABEL_A:=a}" "${LABEL_B:=b}" "${IMAGE_A:=}" "${IMAGE_B:=}" "${REF_A:=}" "${REF_B:=}"
: "${STORAGE_CLASS:=}" "${DATA_SIZE:=20Gi}" "${MINIO_SIZE:=20Gi}" "${KEEP:=0}" "${PULL_SECRET_FILE:=}"
: "${MINIO_IMAGE:?}" "${MINIO_CMD:?}" "${AWS_CLI_IMAGE:?}" "${COMPARE_IMAGE:?}" "${COMPARE_PACKAGES:?}"
: "${PIP_INDEX_URL:=}" "${CLI_ENV:=}" "${RUNNER_CPU:=2}" "${RUNNER_MEMORY:=4Gi}" "${TIMEOUT_MINUTES:=600}"
: "${RESULTS_DIR:=./regression-results/$(date +%Y%m%d-%H%M%S)}"
[[ -f $CASES ]] || { echo "cases file not found: $CASES" >&2; exit 2; }
for x in A B; do
  img=IMAGE_$x ref=REF_$x
  [[ -n ${!img} || -n ${!ref} ]] || { echo "set IMAGE_$x or REF_$x" >&2; exit 2; }
done
[[ $LABEL_A != "$LABEL_B" && $LABEL_A =~ ^[a-z0-9-]+$ && $LABEL_B =~ ^[a-z0-9-]+$ ]] ||
  { echo "LABEL_A and LABEL_B must differ and use only a-z, 0-9 and -" >&2; exit 2; }

oc whoami >/dev/null
ns="$PROJECT_PREFIX-$(date +%Y%m%d%H%M%S)"
mkdir -p "$RESULTS_DIR"
results=$(cd "$RESULTS_DIR" && pwd)
OC=(oc -n "$ns")
bucket=regression

teardown() {
  local rc=$?
  # Deleting the project deletes its events too, and they are often the only trace of why a pod vanished.
  if ((rc != 0)); then
    "${OC[@]}" get events --sort-by=.lastTimestamp >"$results/events.log" 2>&1 || true
    "${OC[@]}" get pods -o wide >"$results/pods.log" 2>&1 || true
  fi
  if [[ $KEEP == 1 ]]; then
    echo "KEEP=1: project $ns is left running; delete it with: oc delete project $ns"
  else
    oc delete project "$ns" --wait=false >/dev/null && echo "deleted project $ns"
  fi
}

oc new-project "$ns" --skip-config-write >/dev/null
trap teardown EXIT
trap 'exit 130' INT TERM
echo "project $ns, results in $results"

pull_secrets=""
if [[ -n $PULL_SECRET_FILE ]]; then
  "${OC[@]}" create secret generic pull --type=kubernetes.io/dockerconfigjson \
    --from-file=.dockerconfigjson="$PULL_SECRET_FILE" >/dev/null
  pull_secrets="imagePullSecrets: [{name: pull}]"
fi
"${OC[@]}" create secret generic minio --from-literal=user="r$(openssl rand -hex 8)" \
  --from-literal=password="$(openssl rand -hex 24)" >/dev/null

sc_line=""
[[ -n $STORAGE_CLASS ]] && sc_line="storageClassName: $STORAGE_CLASS"
for pvc in "data $DATA_SIZE" "minio $MINIO_SIZE"; do
  read -r name size <<<"$pvc"
  "${OC[@]}" apply -f - >/dev/null <<EOF
apiVersion: v1
kind: PersistentVolumeClaim
metadata: {name: $name}
spec:
  accessModes: [ReadWriteOnce]
  $sc_line
  resources: {requests: {storage: $size}}
EOF
done

s3_env='
        - {name: AWS_ACCESS_KEY_ID, valueFrom: {secretKeyRef: {name: minio, key: user}}}
        - {name: AWS_SECRET_ACCESS_KEY, valueFrom: {secretKeyRef: {name: minio, key: password}}}'

read -r -a minio_cmd <<<"$MINIO_CMD"
"${OC[@]}" apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Pod
metadata: {name: minio, labels: {app: minio}}
spec:
  $pull_secrets
  volumes: [{name: data, persistentVolumeClaim: {claimName: minio}}]
  containers:
    - name: minio
      image: $MINIO_IMAGE
      command: [$(printf '"%s",' "${minio_cmd[@]}" | sed 's/,$//')]
      env:
        - {name: MINIO_ROOT_USER, valueFrom: {secretKeyRef: {name: minio, key: user}}}
        - {name: MINIO_ROOT_PASSWORD, valueFrom: {secretKeyRef: {name: minio, key: password}}}
      ports: [{containerPort: 9000}]
      readinessProbe: {httpGet: {path: /minio/health/ready, port: 9000}}
      resources: {requests: {cpu: "1", memory: 1Gi}, limits: {memory: 2Gi}}
      volumeMounts: [{name: data, mountPath: /data}]
---
apiVersion: v1
kind: Service
metadata: {name: minio}
spec:
  selector: {app: minio}
  ports: [{port: 9000, targetPort: 9000}]
EOF

wait_pod() {
  local pod=$1 phase misses=0 deadline=$((SECONDS + TIMEOUT_MINUTES * 60))
  while :; do
    # A run takes hours, so a short API or login blip must not end it; 15 misses in a row (5 minutes) does.
    if phase=$("${OC[@]}" get pod "$pod" -o jsonpath='{.status.phase}' 2>/dev/null); then
      misses=0
    else
      misses=$((misses + 1))
      ((misses < 15)) || { echo "lost contact with the cluster while waiting for $pod" >&2; return 1; }
    fi
    [[ $phase == Succeeded || $phase == Failed ]] && break
    ((SECONDS < deadline)) || { echo "$pod timed out" >&2; return 1; }
    sleep 20
  done
  for try in 1 2 3; do
    "${OC[@]}" logs "$pod" --all-containers >"$results/$pod.log" && break
    ((try < 3)) || return 1
    sleep 20
  done
  "${OC[@]}" delete pod "$pod" --wait=true >/dev/null
  [[ $phase == Succeeded ]] || { echo "$pod failed, see $results/$pod.log" >&2; return 1; }
}

image_for() {
  local x=$1 img ref label tar
  img=IMAGE_$x ref=REF_$x label=LABEL_$x
  if [[ -n ${!img} ]]; then echo "${!img}"; return; fi
  tar=$(mktemp --suffix=.tar)
  git -C "$here/.." archive --format=tar "${!ref}" >"$tar"
  "${OC[@]}" new-build --binary --strategy=docker --name="cli-${!label}" >/dev/null
  "${OC[@]}" patch bc "cli-${!label}" --type=merge \
    -p '{"spec":{"strategy":{"dockerStrategy":{"dockerfilePath":"MergerCli/Dockerfile"}}}}' >/dev/null
  "${OC[@]}" start-build "cli-${!label}" --from-archive="$tar" --wait >"$results/build-${!label}.log" 2>&1 ||
    { echo "build of ${!ref} failed, see $results/build-${!label}.log" >&2; rm -f "$tar"; return 1; }
  rm -f "$tar"
  "${OC[@]}" get istag "cli-${!label}:latest" -o jsonpath='{.image.dockerImageReference}'
}
image_a=$(image_for A)
image_b=$(image_for B)
echo "A ($LABEL_A): $image_a"
echo "B ($LABEL_B): $image_b"

"${OC[@]}" wait --for=condition=Ready pod/minio --timeout=10m >/dev/null

if [[ $SAMPLE == s3://* ]]; then
  : "${SAMPLE_S3_ENDPOINT:?}" "${SAMPLE_S3_ACCESS_KEY_ID:?}" "${SAMPLE_S3_SECRET_ACCESS_KEY:?}"
  envfile=$(mktemp)
  chmod 600 "$envfile"
  printf 'AWS_ACCESS_KEY_ID=%s\nAWS_SECRET_ACCESS_KEY=%s\n' "$SAMPLE_S3_ACCESS_KEY_ID" "$SAMPLE_S3_SECRET_ACCESS_KEY" >"$envfile"
  "${OC[@]}" create secret generic sample --from-env-file="$envfile" >/dev/null
  rm -f "$envfile"
  fetch="aws --endpoint-url $SAMPLE_S3_ENDPOINT s3 cp --recursive --only-show-errors $SAMPLE /data/sample"
  sample_env='envFrom: [{secretRef: {name: sample}}]'
else
  [[ -d $SAMPLE ]] || { echo "SAMPLE is neither s3://... nor a local folder: $SAMPLE" >&2; exit 2; }
  fetch="until [ -f /data/.loaded ]; do sleep 5; done"
  sample_env=""
fi
"${OC[@]}" apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Pod
metadata: {name: load}
spec:
  restartPolicy: Never
  $pull_secrets
  volumes: [{name: data, persistentVolumeClaim: {claimName: data}}]
  initContainers:
    - name: bucket
      image: $AWS_CLI_IMAGE
      command: [sh, -c, "aws --endpoint-url http://minio:9000 s3 mb s3://$bucket"]
      env:$s3_env
        - {name: HOME, value: /tmp}
  containers:
    - name: load
      image: $AWS_CLI_IMAGE
      command: [sh, -c, "rm -rf /data/sample /data/out && mkdir -p /data/sample && $fetch && ls -lR /data/sample"]
      $sample_env
      env: [{name: HOME, value: /tmp}]
      volumeMounts: [{name: data, mountPath: /data}]
EOF
if [[ $SAMPLE != s3://* ]]; then
  "${OC[@]}" wait --for=condition=Ready pod/load --timeout=10m >/dev/null
  # Streamed file by file: oc cp needs tar in the image, and a long transfer can arrive cut short.
  while IFS= read -r -d '' file; do
    rel=${file#./}
    want=$(cd "$SAMPLE" && md5sum <"$rel" | cut -d' ' -f1)
    for try in 1 2 3; do
      "${OC[@]}" exec -i load -c load -- sh -c "mkdir -p \"\$(dirname '/data/sample/$rel')\" && cat > '/data/sample/$rel'" <"$SAMPLE/$rel"
      got=$("${OC[@]}" exec load -c load -- md5sum "/data/sample/$rel" | cut -d' ' -f1)
      [[ $got == "$want" ]] && break
      ((try < 3)) || { echo "upload of $rel kept arriving incomplete" >&2; exit 1; }
    done
  done < <(cd "$SAMPLE" && find . -type f -print0)
  "${OC[@]}" exec load -c load -- touch /data/.loaded
fi
wait_pod load

"${OC[@]}" create configmap harness --from-file=runner.sh="$here/runner.sh" \
  --from-file=compare.py="$here/compare.py" --from-file=cases.tsv="$CASES" >/dev/null

cli_env=""
for kv in $CLI_ENV; do cli_env+=$'\n'"        - {name: \"${kv%%=*}\", value: \"${kv#*=}\"}"; done

for x in A B; do
  label_var=LABEL_$x image_var=image_${x,,}
  label=${!label_var}
  "${OC[@]}" apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Pod
metadata: {name: run-$label}
spec:
  restartPolicy: Never
  $pull_secrets
  volumes:
    - {name: data, persistentVolumeClaim: {claimName: data}}
    - {name: harness, configMap: {name: harness}}
  containers:
    - name: run
      image: ${!image_var}
      command: [bash, /harness/runner.sh]
      env:$s3_env
        - {name: HOME, value: /tmp}
        - {name: CASES, value: /harness/cases.tsv}
        - {name: SAMPLE_DIR, value: /data/sample}
        - {name: OUT_ROOT, value: /data/out/$label}
        - {name: S3_ROOT, value: $label}
        - {name: S3_BUCKET, value: $bucket}
        - {name: THREADS, value: "$THREADS"}
        - {name: BATCH, value: "$BATCH"}
        - {name: S3__url, value: "http://minio:9000"}
        - {name: S3__bucket, value: $bucket}$cli_env
      resources: {requests: {cpu: "$RUNNER_CPU", memory: $RUNNER_MEMORY}, limits: {memory: $RUNNER_MEMORY}}
      volumeMounts:
        - {name: data, mountPath: /data}
        - {name: harness, mountPath: /harness}
EOF
  echo "running cases with $label..."
  wait_pod "run-$label"
done

first_t=${THREADS%% *}
awk -F'\t' -v la="$LABEL_A" -v lb="$LABEL_B" -v ft="$first_t" '
  FNR == 1 { label = (FILENAME ~ "run-" la "\\.log$") ? la : lb }
  $1 == "TARGET" { tgt[label, $2, $3] = $4; cases[$2] = 1; ts[$3] = 1 }
  END {
    for (c in cases) for (t in ts) {
      if ((la, c, t) in tgt && (lb, c, t) in tgt) print c " t" t " " la " vs " lb "\t" tgt[la, c, t] "\t" tgt[lb, c, t]
      if (t == ft) continue
      if ((la, c, t) in tgt && (la, c, ft) in tgt) print c " " la " t" ft " vs t" t "\t" tgt[la, c, ft] "\t" tgt[la, c, t]
      if ((lb, c, t) in tgt && (lb, c, ft) in tgt) print c " " lb " t" ft " vs t" t "\t" tgt[lb, c, ft] "\t" tgt[lb, c, t]
    }
  }' "$results/run-$LABEL_A.log" "$results/run-$LABEL_B.log" | sort >"$results/pairs.tsv"

"${OC[@]}" create configmap pairs --from-file=pairs.tsv="$results/pairs.tsv" >/dev/null
pip_index=""
[[ -n $PIP_INDEX_URL ]] && pip_index="--index-url $PIP_INDEX_URL"
"${OC[@]}" apply -f - >/dev/null <<EOF
apiVersion: v1
kind: Pod
metadata: {name: compare}
spec:
  restartPolicy: Never
  $pull_secrets
  volumes:
    - {name: data, persistentVolumeClaim: {claimName: data}}
    - {name: harness, configMap: {name: harness}}
    - {name: pairs, configMap: {name: pairs}}
  containers:
    - name: compare
      image: $COMPARE_IMAGE
      command:
        - sh
        - -c
        - |
          pip install --quiet --target /tmp/py $pip_index $COMPARE_PACKAGES || exit 1
          export PYTHONPATH=/tmp/py
          python3 -c "import numpy, PIL, boto3" || exit 1
          while IFS="\$(printf '\t')" read -r name a b; do
            echo "### \$name"
            python3 -s /harness/compare.py "\$a" "\$b"
            case \$? in 0) r=MATCH ;; 1) r=MISMATCH ;; *) r=ERROR ;; esac
            printf 'COMPARE\t%s\t%s\n' "\$r" "\$name"
          done < /pairs/pairs.tsv
      env:$s3_env
        - {name: HOME, value: /tmp}
        - {name: S3_ENDPOINT, value: "http://minio:9000"}
      volumeMounts:
        - {name: data, mountPath: /data}
        - {name: harness, mountPath: /harness}
        - {name: pairs, mountPath: /pairs}
EOF
echo "comparing outputs..."
wait_pod compare

{
  echo "A = $LABEL_A ($image_a)"
  echo "B = $LABEL_B ($image_b)"
  echo
  printf 'RUN\tlabel\tcase\tthreads\tstep\tpass\texit\tseconds\ttiles\n'
  for l in "$LABEL_A" "$LABEL_B"; do grep -E '^RESULT' "$results/run-$l.log" | sed "s/^RESULT/RUN\t$l/"; done
  echo
  grep -E '^COMPARE' "$results/compare.log"
} | tee "$results/summary.tsv"

failed_runs=$(grep -c -P '^RUN\t[^\t]+\t[^\t]+\t[^\t]+\t[^\t]+\tno\t' "$results/summary.tsv" || true)
cases_total=$(grep -v -E '^\s*(#|$)' "$CASES" | cut -f1 | sort -u | wc -l)
compares=$(grep -c '^COMPARE' "$results/summary.tsv" || true)
mismatches=$(grep -c -P '^COMPARE\tMISMATCH' "$results/summary.tsv" || true)
compare_errors=$(grep -c -P '^COMPARE\tERROR' "$results/summary.tsv" || true)
echo
echo "failed CLI runs: $failed_runs, comparisons: $compares, mismatches: $mismatches, compare errors: $compare_errors (cases: $cases_total, threads: $THREADS)"
((failed_runs == 0 && mismatches == 0 && compare_errors == 0 && compares > 0))
