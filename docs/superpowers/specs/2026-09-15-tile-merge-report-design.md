# Tile Merge Report — Design

Date: 2026-09-15
Status: Approved for planning

## Goal

Produce a per-task report of what a merge did to the target:

- **Counts**: added, merged (changed), replaced tiles, plus total.
- **Added-tiles list**: exact `z/x/y` of every tile that did not exist in the target
  before the merge.
- Emitted as a **structured log line** (counts + percentages, no list) and a **JSON
  artifact file** (full detail incl. the added list).

Definitions (authoritative):

- **Added** — tile did not exist in the target before the merge.
- **Merged (changed)** — tile existed and the target pixels were blended with source
  pixels (alpha composite).
- **Replaced (full replace)** — tile existed but a fully-opaque source covered it; the
  target tile was not blended in.

## Scope

- **Per task** (the current execution unit in `TaskExecutor.ExecuteTask`). Job-level
  aggregation is a follow-up handled by the job tracker / dashboards.
- No change to merge output tiles themselves — this is observability only.

## Classification

Done inside the coord loop of `TaskExecutor.ExecuteTask`, per coord:

```
existedBefore = !metadata.IsNewTarget && target.TileExists(coord)  // exact coord, no upscale
tile = MergeTiles(builders, coord, strategy, out MergeStats stats)
```

`MergeStats` (new, returned by `MergeTiles`):

- `TargetUsed`  — the target tile was included in the flattened image stack.
- `AnySourceUsed` — at least one source tile contributed data.

Classification:

| existedBefore | tile        | TargetUsed | Result   |
|---------------|-------------|------------|----------|
| —             | `null`      | —          | skipped (no write, counted as `skipped`) |
| false         | non-null    | —          | **Added** (record `z/x/y`) |
| true          | non-null    | true       | **Merged** |
| true          | non-null    | false      | **Replaced** |

Notes / edge cases:

- `TileExists` is skipped when `IsNewTarget` (target created empty → everything is
  Added). This avoids one existence lookup per tile on new targets.
- `TileExists` checks the **exact** coord (no upscale), matching the "exact tile did
  not exist" definition. The target builder in the merge uses upscaling, so its
  non-null result is not a reliable existence signal — hence the separate check.
- A tile where the target exists but no source contributed data
  (`TargetUsed && !AnySourceUsed`) is a target-only re-encode with no real change.
  It is counted as `skipped`, not `merged`. (Whether such tiles should be written at
  all is pre-existing behavior and out of scope.)

## Components

### 1. `MergeStats` + `MergeTiles` signal (MergerLogic/ImageProcessing)

- Add `MergeStats` struct (`TargetUsed`, `AnySourceUsed`).
- Add `ITileMerger.MergeTiles(..., out MergeStats stats)`.
- Keep the existing `Tile? MergeTiles(...)` as a thin wrapper that discards the stats,
  so `MergerCli/Process.cs` and existing `TileMergerTest` are untouched.
- Populate the stats in `GetImageList`: `TargetUsed` = the target builder (index 0)
  produced an image that entered the stack; `AnySourceUsed` = any non-target image
  entered the stack. Respect the opaque short-circuit and `uploadOnly` (target dropped
  → `TargetUsed` always false).

### 2. `MergeReport` accumulator (MergerService)

- Plain object created per task. Fields: `jobId`, `taskId`, `taskType`,
  `targetFormat`, `isNewTarget`, `startTime`, `endTime`, counts
  (`added`, `merged`, `replaced`, `skipped`, `total`), and `List<Coord> addedTiles`.
- Incremented in the sequential coord loop (no locking needed).
- Computes percentages of `total` per category at finalize.
- Includes a schema `version` field for forward compatibility.

### 3. `IReportWriter` (MergerService)

- Serializes the finalized `MergeReport` to JSON and writes it to the configured sink.
- Destination path = `AdditionalParams.ReportOutputPath` on the job object.
- Sink type (S3 vs FS) chosen by service configuration; reuses existing S3/File client
  patterns in `MergerLogic/Clients`.
- If `ReportOutputPath` is absent/empty → skip the artifact; still emit the log line.
- Failure to write the artifact must not fail the task — log an error and continue.

### 4. Wiring

- Add `ReportOutputPath` (nullable string) to
  `MergerService/Models/Jobs/JobParamersAdditiomalParams.cs` (`AdditionalParams`).
- `TaskRunner.RunTask` already reads `job.Parameters.AdditionalParams`; extract
  `ReportOutputPath` there and pass it into `ExecuteTask` alongside `managerCallbackUrl`.
- `TaskExecutor.ExecuteTask` builds and finalizes the `MergeReport`, then calls
  `IReportWriter` and emits the structured log line.

## Report JSON shape (artifact)

```json
{
  "version": 1,
  "jobId": "...",
  "taskId": "...",
  "taskType": "...",
  "targetFormat": "PNG",
  "isNewTarget": false,
  "startTime": "2026-09-15T00:00:00Z",
  "endTime": "2026-09-15T00:01:00Z",
  "durationSeconds": 60,
  "counts": { "added": 10, "merged": 5, "replaced": 2, "skipped": 1, "total": 18 },
  "percentages": { "added": 55.6, "merged": 27.8, "replaced": 11.1, "skipped": 5.6 },
  "addedTiles": [ { "z": 10, "x": 1, "y": 2 } ]
}
```

Log line = same object **without** `addedTiles`.

## Error handling

- Report writing is best-effort: any exception in serialization/sink write is logged
  and swallowed; task success is unaffected.
- Absent `ReportOutputPath` → log line only.

## Testing

- Classification branches (added / merged / replaced / skipped) driven by `MergeStats`
  and `existedBefore`.
- `MergeStats` population in `TileMerger` (target-only, opaque source, blended, uploadOnly).
- `MergeReport` percentage math and totals.
- `IReportWriter`: FS write, S3 write, absent-path skip, write-failure is non-fatal.

## Out of scope

- Job-level aggregation across tasks (tracker/dashboard concern).
- Emitting counts as Prometheus metrics and dashboard work — tracked in a separate
  Jira ticket (see below), which also covers auditing/cleaning existing metrics and
  organizing the dashboards.

## Follow-up ticket (drafted, pending confirmation)

MAPCO ticket to: add added/merged/replaced counts (and percentage-of-total per
category) as Prometheus metrics and to the dashboard; and audit, clean up, and
reorganize existing metrics + dashboards.
