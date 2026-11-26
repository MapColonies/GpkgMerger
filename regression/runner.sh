#!/usr/bin/env bash
# Prints one RESULT line per CLI call and one TARGET line per finished case; the CLI's own output goes to log files.
set -uo pipefail

: "${CASES:?}" "${SAMPLE_DIR:?}" "${OUT_ROOT:?}" "${S3_ROOT:?}" "${THREADS:?}" "${BATCH:?}"
CLI=(dotnet /app/MergerCli.dll MergerCli)

mapfile -t case_names < <(grep -v -E '^\s*(#|$)' "$CASES" | cut -f1 | awk '!seen[$0]++')

for t in $THREADS; do
  for name in "${case_names[@]}"; do
    work="$OUT_ROOT/t$t/$name"
    rm -rf "$work" && mkdir -p "$work"
    # Every case gets a fresh copy: some CLI versions rewrite their source files.
    cp -r "$SAMPLE_DIR" "$work/src"
    step=0 ok=1 args=()
    while IFS=$'\t' read -r case_name line; do
      [[ $case_name == "$name" ]] || continue
      step=$((step + 1))
      line=${line//\{BATCH\}/$BATCH}
      line=${line//\{SRC\}/$work/src}
      line=${line//\{OUT\}/$work}
      line=${line//\{S3\}/$S3_ROOT/t$t/$name}
      read -r -a args <<<"$line"
      log="$work/step$step.log"
      start=$(date +%s)
      (cd "$work" && GENERAL__parallel__numOfThreads=$t GENERAL__resumeOutputFolder=$work "${CLI[@]}" "${args[@]}") >"$log" 2>&1
      rc=$?
      secs=$(($(date +%s) - start))
      errors=$(grep -c -F '] [Error] [' "$log")
      count=$(grep -o -E 'Tile Count: [0-9]+ / [0-9]+' "$log" | tail -1)
      pass=yes
      # The CLI has exited 0 after a failed merge, so the log decides too.
      if ((rc != 0 || errors > 0)); then pass=no; fi
      if [[ $count =~ ([0-9]+)\ /\ ([0-9]+) ]] && [[ ${BASH_REMATCH[1]} != "${BASH_REMATCH[2]}" ]]; then pass=no; fi
      printf 'RESULT\t%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$name" "$t" "$step" "$pass" "$rc" "$secs" "${count:-no tile count}"
      if [[ $pass == no ]]; then
        ok=0
        grep -F '] [Error] [' "$log" | head -3 | cut -c1-300
        break
      fi
    done < <(grep -v -E '^\s*(#|$)' "$CASES")
    rm -rf "$work/src"
    if ((ok)) && ((${#args[@]} >= 3)); then
      target=${args[2]}
      if [[ ${args[1]} == s3 ]]; then target="s3://$S3_BUCKET/$target"; fi
      printf 'TARGET\t%s\t%s\t%s\n' "$name" "$t" "$target"
    fi
  done
done
echo DONE
