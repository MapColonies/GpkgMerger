# MergerCli regression harness

- [TLDR](#tldr)
- [Requirements](#requirements)
- [Run it](#run-it)
- [Cases](#cases)
- [Reading the results](#reading-the-results)
- [What it creates and deletes](#what-it-creates-and-deletes)
- [Limits](#limits)

## TLDR

`regress.sh` runs two MergerCli versions on the same GPKGs in a throwaway OpenShift project and compares their
outputs tile by tile, pixel by pixel. Use it before a release, or on a change that should keep the output the same
(a speed-up, a refactor), to catch differences that unit tests and fixtures miss. It exits 0 only when every CLI run
passed and every output matched.

All settings come from a config file you keep outside the repo; nothing here holds real hosts, buckets or keys.

## Requirements

- `oc whoami` prints your user, and you can create projects (`oc auth can-i create projectrequests` says `yes`).
- `git`, `openssl` and `awk` are on your path.
- The cluster can pull the images named in the config and reach PyPI (or the mirror in `PIP_INDEX_URL`). To build a
  git ref, it must also reach the base images in `MergerCli/Dockerfile`.
- Sample GPKGs, either in an S3 bucket or in a local folder.

## Run it

```bash
cp regression/config.example.env ~/regression.env       # fill in the <...> values
cp regression/cases.example.tsv ~/regression-cases.tsv  # use your sample's file names
export SAMPLE_S3_ACCESS_KEY_ID=... SAMPLE_S3_SECRET_ACCESS_KEY=...  # only for an s3:// sample
regression/regress.sh ~/regression.env
```

A run takes as long as the slower version needs for every case and thread count, plus about 10 minutes of setup.
Run it in `tmux` or with `nohup`, since the project is deleted when the script exits.

Prefer an S3 sample to a local folder: a local folder is streamed through `oc exec`, which is slow and can arrive cut
short over an unstable link. Each file is checked with md5 and resent up to 3 times. File names cannot contain quotes.

## Cases

Each line of the cases file is `<name><TAB><CLI arguments after the program name>`. Lines that share a name run in
order on one target, so a case can build a layer and then update it. The first source after the batch size is the
target, and that is what gets compared. See `cases.example.tsv` for the placeholders.

Every case runs once per entry in `THREADS`. When there is more than one entry, each version's output for each count
is also compared with its output for the first count, which shows output that depends on thread timing.

## Reading the results

Everything lands in `RESULTS_DIR` (default `./regression-results/<time>`):

- `summary.tsv`: one `RUN` line per CLI call (passed, exit code, seconds, tile count) and one `COMPARE` line per pair.
- `run-<label>.log`: each run's result lines, with the first errors of a failed call.
- `compare.log`: each pair's tile counts and the first differing tiles, as `(zoom, x, y)` with the largest channel
  difference.
- `build-<label>.log`: the image build, when a git ref was given.
- `events.log` and `pods.log`: the project's events and pods, saved before it is deleted when a run fails.

A CLI call fails when it exits non-zero, logs an `[Error]`, or stops before its tile count reaches the total. Older
versions exit 0 after a failed merge, so the log is checked too. A failed call skips the rest of its case and leaves
it out of the comparisons.

## What it creates and deletes

In the project `<PROJECT_PREFIX>-<timestamp>`: two PVCs (data and MinIO), a MinIO pod with random credentials, the
build configs and image streams for git refs, and one pod per step. The whole project is deleted when the script
exits, even on failure or Ctrl-C, unless `KEEP=1`. Nothing outside the project is created or changed; an `s3://`
sample is only read.

## Limits

- It runs the CLI only. The merger service's jobs and tasks are not exercised.
- Case arguments and `CLI_ENV` values cannot contain spaces.
- Timings come from one run on a shared cluster, so treat small differences as noise.
