# Tile Merge Report Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a per-task report of tiles added / merged / replaced during a merge, emitted as a structured log line (counts + percentages) and a JSON artifact file (full detail incl. exact added `z/x/y`).

**Architecture:** `TileMerger` reports whether the target and/or any source contributed to each merged tile via a new `MergeStats` out-param. `TaskExecutor` combines that with an exact-coord `TileExists` pre-check to classify every tile, accumulates counts + the added-tile list into a `MergeReport`, then writes a JSON artifact through `IReportWriter` (FS or S3 by config) and logs a summary. Report destination path comes from a new `AdditionalParams.ReportOutputPath` job field.

**Tech Stack:** C#/.NET, MSTest + Moq (loose `MockRepository`), Newtonsoft.Json, `System.IO.Abstractions` (`IFileSystem`), AWS SDK (`IAmazonS3`).

**Spec:** `docs/superpowers/specs/2026-09-15-tile-merge-report-design.md`

---

## File Structure

- Create `MergerLogic/ImageProcessing/MergeStats.cs` — struct carrying `TargetUsed`, `AnySourceUsed`.
- Modify `MergerLogic/ImageProcessing/ITileMerger.cs` — add `MergeTiles(..., out MergeStats stats)` overload.
- Modify `MergerLogic/ImageProcessing/TileMerger.cs` — populate stats; keep old method as wrapper.
- Create `MergerService/Models/Reports/MergeReport.cs` — accumulator + finalize + serialization.
- Create `MergerService/Utils/IReportWriter.cs` + `MergerService/Utils/ReportWriter.cs` — FS/S3 sink writer.
- Modify `MergerService/Models/Jobs/JobParamersAdditiomalParams.cs` — add `ReportOutputPath`.
- Modify `MergerService/Runners/ITaskExecutor.cs` + `TaskExecutor.cs` — classification, accumulation, emit.
- Modify `MergerService/Runners/TaskRunner.cs` — extract `ReportOutputPath`, pass through.
- Modify `MergerService/Program.cs` — register `IReportWriter`.
- Modify `MergerService/appsettings.json` — add `REPORT` config section.
- Tests: `MergerLogicUnitTests/ImageProcessing/TileMergerTest.cs`, `MergerServiceUnitTests/Models/MergeReportTest.cs`, `MergerServiceUnitTests/Utils/ReportWriterTest.cs`, `MergerServiceUnitTests/Runners/TaskExecutorTest.cs`.

**Global commands**
- Build: `dotnet build GpkgMerger.sln`
- Test one class: `dotnet test --filter "ClassName~<Name>"`

---

## Task 1: `MergeStats` struct

**Files:**
- Create: `MergerLogic/ImageProcessing/MergeStats.cs`

- [ ] **Step 1: Create the struct**

```csharp
namespace MergerLogic.ImageProcessing
{
    /// <summary>
    /// Describes which inputs contributed to a merged tile, used to classify the
    /// write as added / merged / replaced. TargetUsed is false in upload-only mode.
    /// </summary>
    public readonly struct MergeStats
    {
        public bool TargetUsed { get; }
        public bool AnySourceUsed { get; }

        public MergeStats(bool targetUsed, bool anySourceUsed)
        {
            this.TargetUsed = targetUsed;
            this.AnySourceUsed = anySourceUsed;
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build GpkgMerger.sln`
Expected: succeeds.

- [ ] **Step 3: Commit**

```bash
git add MergerLogic/ImageProcessing/MergeStats.cs
git commit -m "feat: add MergeStats to describe merge tile provenance"
```

---

## Task 2: `MergeTiles` exposes `MergeStats`

The target builder is index 0 of the `tiles` list. `GetImageList` iterates from the last source down to the target and short-circuits on the first fully-opaque tile. We must record whether the target image and any source image entered the stack.

**Files:**
- Modify: `MergerLogic/ImageProcessing/ITileMerger.cs`
- Modify: `MergerLogic/ImageProcessing/TileMerger.cs`
- Test: `MergerLogicUnitTests/ImageProcessing/TileMergerTest.cs`

- [ ] **Step 1: Write failing tests for the stats out-param**

Add to `TileMergerTest.cs` (inside the class):

```csharp
[TestMethod]
[TestCategory("unit")]
[TestCategory("MergeTiles")]
public void MergeTilesStats_BlendedTargetAndSource_TargetAndSourceUsed()
{
    // transparent source over an existing target -> both contribute
    var target = new Tile(new Coord(0, 0, 0), this.GetTransparentPngBytes());
    var source = new Tile(new Coord(0, 0, 0), this.GetTransparentPngBytes());
    var builders = new List<CorrespondingTileBuilder> { () => target, () => source };

    this._testTileMerger.MergeTiles(builders, new Coord(0, 0, 0),
        new TileFormatStrategy(TileFormat.Png), out MergeStats stats, uploadOnly: false);

    Assert.IsTrue(stats.TargetUsed);
    Assert.IsTrue(stats.AnySourceUsed);
}

[TestMethod]
[TestCategory("unit")]
[TestCategory("MergeTiles")]
public void MergeTilesStats_OpaqueSource_TargetNotUsed()
{
    var target = new Tile(new Coord(0, 0, 0), this.GetTransparentPngBytes());
    var opaqueSource = new Tile(new Coord(0, 0, 0), this.GetOpaquePngBytes());
    var builders = new List<CorrespondingTileBuilder> { () => target, () => opaqueSource };

    this._testTileMerger.MergeTiles(builders, new Coord(0, 0, 0),
        new TileFormatStrategy(TileFormat.Png), out MergeStats stats, uploadOnly: false);

    Assert.IsFalse(stats.TargetUsed);
    Assert.IsTrue(stats.AnySourceUsed);
}

[TestMethod]
[TestCategory("unit")]
[TestCategory("MergeTiles")]
public void MergeTilesStats_UploadOnly_TargetNotUsed()
{
    var target = new Tile(new Coord(0, 0, 0), this.GetOpaquePngBytes());
    var source = new Tile(new Coord(0, 0, 0), this.GetOpaquePngBytes());
    var builders = new List<CorrespondingTileBuilder> { () => target, () => source };

    this._testTileMerger.MergeTiles(builders, new Coord(0, 0, 0),
        new TileFormatStrategy(TileFormat.Png), out MergeStats stats, uploadOnly: true);

    Assert.IsFalse(stats.TargetUsed);
    Assert.IsTrue(stats.AnySourceUsed);
}
```

Add these helpers to the test class if not already present (reuse existing test image bytes/fixtures in `Runners/TestData` or the existing `TileMergerTest` fixtures if they already expose transparent/opaque tiles — prefer the existing fixtures and delete these helpers if duplicative):

```csharp
private byte[] GetTransparentPngBytes() =>
    File.ReadAllBytes(Path.Combine("TestData", "transparent.png"));
private byte[] GetOpaquePngBytes() =>
    File.ReadAllBytes(Path.Combine("TestData", "opaque.png"));
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "ClassName~TileMergerTest&TestCategory=MergeTiles"`
Expected: FAIL to compile — no `MergeTiles` overload with `out MergeStats`.

- [ ] **Step 3: Add the interface overload**

Edit `ITileMerger.cs`:

```csharp
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public interface ITileMerger
    {
        Tile? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords, TileFormatStrategy strategy, bool uploadOnly = false);

        Tile? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords, TileFormatStrategy strategy,
            out MergeStats stats, bool uploadOnly = false);
    }
}
```

- [ ] **Step 4: Implement in `TileMerger.cs`**

Replace the existing `MergeTiles` and `GetImageList` so provenance is tracked. Keep the old signature as a wrapper.

```csharp
public Tile? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords, TileFormatStrategy strategy, bool uploadOnly = false)
{
    return this.MergeTiles(tiles, targetCoords, strategy, out _, uploadOnly);
}

public Tile? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords, TileFormatStrategy strategy,
    out MergeStats stats, bool uploadOnly = false)
{
    bool targetUsed = false;
    bool anySourceUsed = false;

    if (uploadOnly)
    {
        this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] Configured to upload only mode");
        // Ignore target in upload only mode
        tiles = tiles.Skip(1).ToList();

        if (tiles.Count == 1)
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] Only one source was found, using raw image");
            Tile? rawTile = tiles[0]();
            rawTile?.ConvertToFormat(strategy.ApplyStrategy(rawTile.Format));
            stats = new MergeStats(false, rawTile != null);
            return rawTile;
        }
    }

    // hasTarget is true when the target builder (index 0) is still part of the list
    bool hasTarget = !uploadOnly && tiles.Count > 0;
    var images = this.GetImageList(tiles, targetCoords, uploadOnly, hasTarget, out targetUsed, out anySourceUsed);
    IMagickImage<byte> image;

    switch (images.Count)
    {
        case 0:
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] No images where found return null");
            stats = new MergeStats(targetUsed, anySourceUsed);
            return null;
        case 1:
            ImageFormatter.RemoveImageDateAttributes(images[0]);
            image = images[0];
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] 1 image found");
            break;
        default:
            using (var imageCollection = new MagickImageCollection())
            {
                for (var i = images.Count - 1; i >= 0; i--)
                {
                    imageCollection.Add(images[i]);
                }

                this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] {imageCollection.Count} where found for merge, start 'imageMagic' merging");
                using (var mergedImage = imageCollection.Flatten(MagickColor.FromRgba(0, 0, 0, 0)))
                {
                    ImageFormatter.RemoveImageDateAttributes(mergedImage);
                    mergedImage.ColorSpace = ColorSpace.sRGB;
                    mergedImage.ColorType = mergedImage.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
                    image = new MagickImage(mergedImage);
                    this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] 'imageMagic' merging finished");
                }
            }
            break;
    }

    Tile tile = new Tile(targetCoords, image);
    image.Dispose();
    tile.ConvertToFormat(strategy.ApplyStrategy(tile.Format));
    stats = new MergeStats(targetUsed, anySourceUsed);
    return tile;
}
```

Update `GetImageList` to report provenance. The loop index `i == 0` corresponds to the target when `hasTarget` is true.

```csharp
private List<MagickImage> GetImageList(List<CorrespondingTileBuilder> tiles, Coord targetCoords, bool uploadOnly,
    bool hasTarget, out bool targetUsed, out bool anySourceUsed)
{
    var images = new List<MagickImage>();
    int i = tiles.Count - 1;
    Tile? tile = null;
    targetUsed = false;
    anySourceUsed = false;

    bool hasAlpha = false;
    try
    {
        for (; i >= 0; i--)
        {
            // protect in case all "sources" tiles are null
            if (images.Count == 0 && i == 0 && !uploadOnly)
            {
                this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] All sources are empty - return");
                return images;
            }

            tile = tiles[i]();
            if (tile is null)
            {
                continue;
            }

            int before = images.Count;
            this.AddTileToImageList(targetCoords, tile, images, out hasAlpha);
            bool added = images.Count > before;
            if (added)
            {
                if (hasTarget && i == 0)
                {
                    targetUsed = true;
                }
                else
                {
                    anySourceUsed = true;
                }
            }

            if (!hasAlpha)
            {
                return images;
            }
        }
    }
    catch
    {
        images.ForEach(image => image.Dispose());
        throw;
    }

    return images;
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "ClassName~TileMergerTest&TestCategory=MergeTiles"`
Expected: PASS (all MergeTiles tests, including pre-existing ones).

- [ ] **Step 6: Commit**

```bash
git add MergerLogic/ImageProcessing/ITileMerger.cs MergerLogic/ImageProcessing/TileMerger.cs MergerLogicUnitTests/ImageProcessing/TileMergerTest.cs
git commit -m "feat: expose target/source provenance from MergeTiles via MergeStats"
```

---

## Task 3: `MergeReport` accumulator

**Files:**
- Create: `MergerService/Models/Reports/MergeReport.cs`
- Test: `MergerServiceUnitTests/Models/MergeReportTest.cs`

- [ ] **Step 1: Write failing tests**

Create `MergerServiceUnitTests/Models/MergeReportTest.cs`:

```csharp
using MergerLogic.DataTypes;
using MergerService.Models.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;

namespace MergerServiceUnitTests.Models
{
    [TestClass]
    [TestCategory("unit")]
    public class MergeReportTest
    {
        [TestMethod]
        public void Counts_And_Percentages_Are_Computed()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            report.RecordAdded(new Coord(10, 1, 2));
            report.RecordAdded(new Coord(10, 1, 3));
            report.RecordMerged();
            report.RecordReplaced();
            report.RecordSkipped();

            report.Finalize(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                            new DateTime(2026, 9, 15, 0, 1, 0, DateTimeKind.Utc));

            Assert.AreEqual(2, report.Added);
            Assert.AreEqual(1, report.Merged);
            Assert.AreEqual(1, report.Replaced);
            Assert.AreEqual(1, report.Skipped);
            Assert.AreEqual(5, report.Total);
            Assert.AreEqual(60, report.DurationSeconds);
            Assert.AreEqual(40.0, report.AddedPercentage, 0.01);
        }

        [TestMethod]
        public void Json_Includes_AddedTiles_LogString_Excludes_Them()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            report.RecordAdded(new Coord(10, 1, 2));
            report.Finalize(DateTime.UnixEpoch, DateTime.UnixEpoch);

            JObject json = JObject.Parse(report.ToJson());
            Assert.AreEqual(1, ((JArray)json["addedTiles"]).Count);

            Assert.IsFalse(report.ToLogString().Contains("addedTiles"));
            Assert.IsTrue(report.ToLogString().Contains("\"added\""));
        }

        [TestMethod]
        public void Percentages_Are_Zero_When_No_Tiles()
        {
            var report = new MergeReport("job1", "task1", "MERGE", "PNG", true);
            report.Finalize(DateTime.UnixEpoch, DateTime.UnixEpoch);
            Assert.AreEqual(0, report.Total);
            Assert.AreEqual(0.0, report.AddedPercentage, 0.01);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter "ClassName~MergeReportTest"`
Expected: FAIL to compile — `MergeReport` does not exist.

- [ ] **Step 3: Implement `MergeReport`**

Create `MergerService/Models/Reports/MergeReport.cs`:

```csharp
using MergerLogic.DataTypes;
using Newtonsoft.Json;

namespace MergerService.Models.Reports
{
    public class MergeReport
    {
        public int Version => 1;
        public string JobId { get; }
        public string TaskId { get; }
        public string TaskType { get; }
        public string TargetFormat { get; }
        public bool IsNewTarget { get; }

        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public double DurationSeconds { get; private set; }

        public int Added { get; private set; }
        public int Merged { get; private set; }
        public int Replaced { get; private set; }
        public int Skipped { get; private set; }
        public int Total => this.Added + this.Merged + this.Replaced + this.Skipped;

        public double AddedPercentage { get; private set; }
        public double MergedPercentage { get; private set; }
        public double ReplacedPercentage { get; private set; }
        public double SkippedPercentage { get; private set; }

        public List<Coord> AddedTiles { get; } = new List<Coord>();

        public MergeReport(string jobId, string taskId, string taskType, string targetFormat, bool isNewTarget)
        {
            this.JobId = jobId;
            this.TaskId = taskId;
            this.TaskType = taskType;
            this.TargetFormat = targetFormat;
            this.IsNewTarget = isNewTarget;
        }

        public void RecordAdded(Coord coord)
        {
            this.Added++;
            this.AddedTiles.Add(coord);
        }

        public void RecordMerged() => this.Merged++;
        public void RecordReplaced() => this.Replaced++;
        public void RecordSkipped() => this.Skipped++;

        public void Finalize(DateTime startTime, DateTime endTime)
        {
            this.StartTime = startTime;
            this.EndTime = endTime;
            this.DurationSeconds = (endTime - startTime).TotalSeconds;

            int total = this.Total;
            if (total > 0)
            {
                this.AddedPercentage = 100.0 * this.Added / total;
                this.MergedPercentage = 100.0 * this.Merged / total;
                this.ReplacedPercentage = 100.0 * this.Replaced / total;
                this.SkippedPercentage = 100.0 * this.Skipped / total;
            }
        }

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None);

        // Summary for the structured log line: counts + percentages, WITHOUT the added-tile list.
        public string ToLogString()
        {
            var summary = new
            {
                version = this.Version,
                jobId = this.JobId,
                taskId = this.TaskId,
                taskType = this.TaskType,
                targetFormat = this.TargetFormat,
                isNewTarget = this.IsNewTarget,
                durationSeconds = this.DurationSeconds,
                counts = new { added = this.Added, merged = this.Merged, replaced = this.Replaced, skipped = this.Skipped, total = this.Total },
                percentages = new { added = this.AddedPercentage, merged = this.MergedPercentage, replaced = this.ReplacedPercentage, skipped = this.SkippedPercentage }
            };
            return JsonConvert.SerializeObject(summary, Formatting.None);
        }
    }
}
```

Note: `ToJson()` serializes `AddedTiles` (property name `addedTiles` via camelCase is NOT applied by default in Newtonsoft — the test parses `json["addedTiles"]`). To match, add `[JsonProperty("addedTiles")]` on `AddedTiles` and `[JsonProperty("added")]` etc. only where the test asserts specific names. Concretely add these attributes:

```csharp
[JsonProperty("addedTiles")] public List<Coord> AddedTiles { get; } = new List<Coord>();
```

And in `ToJson()` the top-level `added` count is asserted in the log-string test only (which uses the anonymous object). For `ToJson` the test only checks `addedTiles`, so the single attribute above is sufficient.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --filter "ClassName~MergeReportTest"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MergerService/Models/Reports/MergeReport.cs MergerServiceUnitTests/Models/MergeReportTest.cs
git commit -m "feat: add MergeReport accumulator with counts, percentages and added-tile list"
```

---

## Task 4: `IReportWriter` + `ReportWriter` (FS/S3 sink)

Writer serializes a `MergeReport` and writes it to a file named `merge-report-{jobId}-{taskId}.json` under `outputPath`. Sink chosen by config `REPORT:sink` (`"FS"` or `"S3"`). For S3 the bucket comes from `S3:bucket` and `outputPath` is the key prefix. Absent/empty `outputPath` → no-op. Write failures throw (caller decides how to handle — see the open question in the spec).

**Files:**
- Create: `MergerService/Utils/IReportWriter.cs`
- Create: `MergerService/Utils/ReportWriter.cs`
- Test: `MergerServiceUnitTests/Utils/ReportWriterTest.cs`

- [ ] **Step 1: Write failing tests**

Create `MergerServiceUnitTests/Utils/ReportWriterTest.cs`:

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Utils;
using MergerService.Models.Reports;
using MergerService.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Threading;
using System.Threading.Tasks;

namespace MergerServiceUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    public class ReportWriterTest
    {
        private Mock<IConfigurationManager> _config;
        private Mock<ILogger<ReportWriter>> _logger;
        private Mock<IAmazonS3> _s3;

        [TestInitialize]
        public void BeforeEach()
        {
            this._config = new Mock<IConfigurationManager>(MockBehavior.Loose);
            this._logger = new Mock<ILogger<ReportWriter>>(MockBehavior.Loose);
            this._s3 = new Mock<IAmazonS3>(MockBehavior.Loose);
        }

        private MergeReport BuildReport()
        {
            var r = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            r.Finalize(System.DateTime.UnixEpoch, System.DateTime.UnixEpoch);
            return r;
        }

        [TestMethod]
        public void FsSink_WritesJsonFile()
        {
            this._config.Setup(c => c.GetConfiguration("REPORT", "sink")).Returns("FS");
            var fs = new MockFileSystem();
            var writer = new ReportWriter(this._config.Object, fs, this._s3.Object, this._logger.Object);

            writer.WriteReport(this.BuildReport(), "/reports");

            string expected = fs.Path.Combine("/reports", "merge-report-job1-task1.json");
            Assert.IsTrue(fs.FileExists(expected));
            StringAssert.Contains(fs.File.ReadAllText(expected), "\"jobId\":\"job1\"");
        }

        [TestMethod]
        public void S3Sink_PutsObject()
        {
            this._config.Setup(c => c.GetConfiguration("REPORT", "sink")).Returns("S3");
            this._config.Setup(c => c.GetConfiguration("S3", "bucket")).Returns("tiles");
            this._s3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PutObjectResponse());
            var fs = new MockFileSystem();
            var writer = new ReportWriter(this._config.Object, fs, this._s3.Object, this._logger.Object);

            writer.WriteReport(this.BuildReport(), "reports");

            this._s3.Verify(s => s.PutObjectAsync(
                It.Is<PutObjectRequest>(r => r.BucketName == "tiles" && r.Key == "reports/merge-report-job1-task1.json"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void EmptyPath_IsNoOp()
        {
            var fs = new MockFileSystem();
            var writer = new ReportWriter(this._config.Object, fs, this._s3.Object, this._logger.Object);

            writer.WriteReport(this.BuildReport(), null);
            writer.WriteReport(this.BuildReport(), "");

            Assert.AreEqual(0, fs.AllFiles.Count());
            this._s3.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
```

Ensure the test project references `System.IO.Abstractions.TestingHelpers` (already used elsewhere in the suite; if the package is missing, add `<PackageReference Include="System.IO.Abstractions.TestingHelpers" Version="17.0.24" />` to `MergerServiceUnitTests.csproj` matching the version of `System.IO.Abstractions` already referenced).

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter "ClassName~ReportWriterTest"`
Expected: FAIL to compile — `IReportWriter`/`ReportWriter` do not exist.

- [ ] **Step 3: Implement interface**

Create `MergerService/Utils/IReportWriter.cs`:

```csharp
using MergerService.Models.Reports;

namespace MergerService.Utils
{
    public interface IReportWriter
    {
        // Writes the report JSON artifact to the configured sink under outputPath.
        // No-op when outputPath is null/empty. Throws on write failure.
        void WriteReport(MergeReport report, string? outputPath);
    }
}
```

- [ ] **Step 4: Implement writer**

Create `MergerService/Utils/ReportWriter.cs`:

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Utils;
using MergerService.Models.Reports;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Reflection;
using System.Text;

namespace MergerService.Utils
{
    public class ReportWriter : IReportWriter
    {
        private readonly IConfigurationManager _configuration;
        private readonly IFileSystem _fileSystem;
        private readonly IAmazonS3 _s3;
        private readonly ILogger<ReportWriter> _logger;

        public ReportWriter(IConfigurationManager configuration, IFileSystem fileSystem, IAmazonS3 s3,
            ILogger<ReportWriter> logger)
        {
            this._configuration = configuration;
            this._fileSystem = fileSystem;
            this._s3 = s3;
            this._logger = logger;
        }

        public void WriteReport(MergeReport report, string? outputPath)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            if (string.IsNullOrEmpty(outputPath))
            {
                this._logger.LogDebug($"[{methodName}] No ReportOutputPath configured, skipping report artifact");
                return;
            }

            string fileName = $"merge-report-{report.JobId}-{report.TaskId}.json";
            string json = report.ToJson();
            string sink = this._configuration.GetConfiguration("REPORT", "sink");

            if (string.Equals(sink, "S3", System.StringComparison.OrdinalIgnoreCase))
            {
                this.WriteToS3(outputPath, fileName, json);
            }
            else
            {
                this.WriteToFs(outputPath, fileName, json);
            }

            this._logger.LogInformation($"[{methodName}] Wrote merge report to {sink}:{outputPath}/{fileName}");
        }

        private void WriteToFs(string outputPath, string fileName, string json)
        {
            this._fileSystem.Directory.CreateDirectory(outputPath);
            string fullPath = this._fileSystem.Path.Combine(outputPath, fileName);
            this._fileSystem.File.WriteAllText(fullPath, json);
        }

        private void WriteToS3(string outputPath, string fileName, string json)
        {
            string bucket = this._configuration.GetConfiguration("S3", "bucket");
            string key = $"{outputPath.TrimEnd('/')}/{fileName}";
            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                ContentBody = json,
                ContentType = "application/json"
            };
            var res = this._s3.PutObjectAsync(request).Result;
        }
    }
}
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test --filter "ClassName~ReportWriterTest"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add MergerService/Utils/IReportWriter.cs MergerService/Utils/ReportWriter.cs MergerServiceUnitTests/Utils/ReportWriterTest.cs
git commit -m "feat: add ReportWriter with FS and S3 sinks for merge report artifact"
```

---

## Task 5: `ReportOutputPath` on `AdditionalParams`

**Files:**
- Modify: `MergerService/Models/Jobs/JobParamersAdditiomalParams.cs`

- [ ] **Step 1: Add the nullable field + ctor param**

Edit `AdditionalParams` to add the property and an optional constructor parameter (optional so existing construction sites and deserialization keep working):

```csharp
public class AdditionalParams
{
    [JsonInclude] public string? JobTrackerServiceURL { get; }
    [JsonInclude] public string? ReportOutputPath { get; }

    [System.Text.Json.Serialization.JsonIgnore]
    private JsonSerializerSettings _jsonSerializerSettings;

    public AdditionalParams(string jobTrackerServiceURL, string? reportOutputPath = null)
    {
        this.JobTrackerServiceURL = jobTrackerServiceURL;
        this.ReportOutputPath = reportOutputPath;

        this._jsonSerializerSettings = new JsonSerializerSettings();
        this._jsonSerializerSettings.Converters.Add(new StringEnumConverter());
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build GpkgMerger.sln`
Expected: succeeds.

- [ ] **Step 3: Commit**

```bash
git add MergerService/Models/Jobs/JobParamersAdditiomalParams.cs
git commit -m "feat: add ReportOutputPath to job AdditionalParams"
```

---

## Task 6: Classify, accumulate, and emit in `TaskExecutor`

Add `IReportWriter` dependency, extend `ExecuteTask` with `reportOutputPath`, build a `MergeReport`, classify each tile, then finalize + log + write.

**Files:**
- Modify: `MergerService/Runners/ITaskExecutor.cs`
- Modify: `MergerService/Runners/TaskExecutor.cs`
- Test: `MergerServiceUnitTests/Runners/TaskExecutorTest.cs`

- [ ] **Step 1: Write a failing classification test**

Add to `TaskExecutorTest.cs`. This drives a 1-tile batch where the target already exists and the merged tile uses the target → expect one `RecordMerged` and a report artifact write. Use the existing test's mock wiring for `IDataFactory`/`IData`; add an `IReportWriter` mock and assert it is called once with a report whose `Merged == 1`.

```csharp
[TestMethod]
[TestCategory("unit")]
[TestCategory("runners")]
public void ExecuteTask_ExistingTargetTileBlended_CountsAsMerged()
{
    // Arrange: target.TileExists(coord) == true, MergeTiles returns a tile with TargetUsed=true.
    var reportWriterMock = this._mockRepository.Create<IReportWriter>();
    MergeReport captured = null;
    reportWriterMock
        .Setup(w => w.WriteReport(It.IsAny<MergeReport>(), "reports"))
        .Callback<MergeReport, string>((r, p) => captured = r);

    var target = new Mock<IData>(MockBehavior.Loose);
    target.SetupGet(t => t.Type).Returns(DataType.GPKG);
    target.Setup(t => t.TileExists(It.IsAny<Coord>())).Returns(true);
    target.Setup(t => t.GetCorrespondingTile(It.IsAny<Coord>(), It.IsAny<bool>()))
          .Returns(new Tile(new Coord(0, 0, 0), this.GetTransparentPngBytes()));
    // ... wire _dataFactoryMock.CreateDataSource(...) to return target for a single-source task
    //     and metadata with IsNewTarget=false and a single 1x1 batch at z0.

    var executor = this.BuildExecutor(reportWriterMock.Object); // helper that constructs TaskExecutor with all mocks

    // Act
    executor.ExecuteTask(BuildSingleTileTask(isNewTarget: false), this._taskUtilsMock.Object, null, "reports");

    // Assert
    reportWriterMock.Verify(w => w.WriteReport(It.IsAny<MergeReport>(), "reports"), Times.Once);
    Assert.IsNotNull(captured);
    Assert.AreEqual(1, captured.Merged);
    Assert.AreEqual(0, captured.Added);
}
```

Add two sibling tests mirroring this exactly but for the other branches (repeat the arrange block; do not cross-reference):

- `ExecuteTask_NewTargetTile_CountsAsAdded`: `target.TileExists` returns `false` (or `IsNewTarget=true`), `MergeTiles` yields `AnySourceUsed=true`, `TargetUsed=false`; assert `captured.Added == 1`, `captured.AddedTiles.Count == 1`.
- `ExecuteTask_OpaqueSourceOverExisting_CountsAsReplaced`: `target.TileExists` returns `true`, `MergeTiles` yields `TargetUsed=false`, `AnySourceUsed=true`; assert `captured.Replaced == 1`.

To make the merge outcome deterministic in these tests, mock `ITileMerger` instead of using the real one, so the test controls the returned `MergeStats`:

```csharp
this._tileMergerMock = this._mockRepository.Create<ITileMerger>();
MergeStats outStats = new MergeStats(targetUsed: false, anySourceUsed: true);
this._tileMergerMock
    .Setup(m => m.MergeTiles(It.IsAny<List<CorrespondingTileBuilder>>(), It.IsAny<Coord>(),
        It.IsAny<TileFormatStrategy>(), out outStats, It.IsAny<bool>()))
    .Returns(new Tile(new Coord(0, 0, 0), this.GetTransparentPngBytes()));
```

(Replace the currently-used real `_testTileMerger` in the executor construction with `_tileMergerMock.Object` for these tests.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter "ClassName~TaskExecutorTest&TestCategory=runners"`
Expected: FAIL to compile — `ExecuteTask` has no `reportOutputPath` param and `TaskExecutor` ctor has no `IReportWriter`.

- [ ] **Step 3: Update the interface**

Edit `ITaskExecutor.cs`:

```csharp
using MergerService.Models.Tasks;
using MergerService.Utils;

namespace MergerService.Runners
{
  public interface ITaskExecutor
  {
    void ExecuteTask(MergeTask task, ITaskUtils taskUtils, string? managerCallbackUrl, string? reportOutputPath);
  }
}
```

- [ ] **Step 4: Wire the writer + report into `TaskExecutor`**

In `TaskExecutor.cs`:

1. Add field + ctor param:

```csharp
private readonly IReportWriter _reportWriter;
```

Add `IReportWriter reportWriter` to the constructor signature and assign `this._reportWriter = reportWriter;`.

2. Add `using MergerService.Models.Reports;` at the top.

3. Change the method signature:

```csharp
public void ExecuteTask(MergeTask task, ITaskUtils taskUtils, string? managerCallbackUrl, string? reportOutputPath)
```

4. After `MergeMetadata metadata = task.Parameters;` create the report and capture start time:

```csharp
DateTime reportStart = DateTime.UtcNow;
MergeReport report = new MergeReport(task.JobId, task.Id, task.Type,
    metadata.TargetFormat.ToString(), metadata.IsNewTarget);
```

5. Replace the per-coord merge block. Currently:

```csharp
var tileMergeStopwatch = Stopwatch.StartNew();
Tile? tile = this._tileMerger.MergeTiles(correspondingTileBuilders, coord, strategy, metadata.IsNewTarget);
tileMergeStopwatch.Stop();
this._metricsProvider.MergeTimePerTileHistogram(tileMergeStopwatch.Elapsed.TotalSeconds, metadata.TargetFormat);

if (tile != null)
{
    tiles.Add(tile);
    currentBatchBytes += tile.Size();
    ...
}
```

Change to classify before/after merge:

```csharp
bool existedBefore = !metadata.IsNewTarget && target.TileExists(coord);

var tileMergeStopwatch = Stopwatch.StartNew();
Tile? tile = this._tileMerger.MergeTiles(correspondingTileBuilders, coord, strategy, out MergeStats stats, metadata.IsNewTarget);
tileMergeStopwatch.Stop();
this._metricsProvider.MergeTimePerTileHistogram(tileMergeStopwatch.Elapsed.TotalSeconds, metadata.TargetFormat);

if (tile != null)
{
    if (!stats.AnySourceUsed)
    {
        // target re-encode with no source data — not a real change
        report.RecordSkipped();
    }
    else if (!existedBefore)
    {
        report.RecordAdded(coord);
    }
    else if (stats.TargetUsed)
    {
        report.RecordMerged();
    }
    else
    {
        report.RecordReplaced();
    }

    tiles.Add(tile);
    currentBatchBytes += tile.Size();

    if (currentBatchBytes >= this._batchMaxBytes || (this._limitBatchSize && tiles.Count >= this._batchMaxSize))
    {
        this.UpdateTargetTiles(target, tiles, task, overallTileProgressCount, totalTileCount, taskUtils);
        tiles.Clear();
        currentBatchBytes = 0;
    }
}
else
{
    report.RecordSkipped();
}
```

6. After `target.Wrapup();` (still inside `ExecuteTask`, before the final debug log), finalize + emit:

```csharp
report.Finalize(reportStart, DateTime.UtcNow);
this._logger.LogInformation($"[{methodName}] Merge report: {report.ToLogString()}");
try
{
    this._reportWriter.WriteReport(report, reportOutputPath);
}
catch (Exception e)
{
    // Best-effort (proposed default). Whether this should fail the task is an open
    // question raised on the implementation PR.
    this._logger.LogError(e, $"[{methodName}] Failed to write merge report artifact: {e.Message}");
}
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test --filter "ClassName~TaskExecutorTest&TestCategory=runners"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add MergerService/Runners/ITaskExecutor.cs MergerService/Runners/TaskExecutor.cs MergerServiceUnitTests/Runners/TaskExecutorTest.cs
git commit -m "feat: classify added/merged/replaced tiles and emit merge report in TaskExecutor"
```

---

## Task 7: Pass `ReportOutputPath` through `TaskRunner`

**Files:**
- Modify: `MergerService/Runners/TaskRunner.cs`
- Test: `MergerServiceUnitTests/Runners/TaskRunnerTest.cs`

- [ ] **Step 1: Write/adjust failing test**

`RunTask` currently calls `this._taskExecutor.ExecuteTask(task, this._taskUtils, managerCallbackUrl)`. The `ITaskExecutor` change (Task 6) breaks compilation of `TaskRunner` and its tests. Update the `TaskRunnerTest` verification of `ExecuteTask` to include the new argument, and add an assertion that the `ReportOutputPath` from the job flows through:

```csharp
// In the existing "happy path" RunTask test where a job is returned by _jobUtils:
this._taskExecutorMock.Verify(e => e.ExecuteTask(task, this._taskUtils, It.IsAny<string>(), "reports"), Times.Once);
```

Set the mocked job's `AdditionalParams.ReportOutputPath` to `"reports"` in that test's job fixture.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter "ClassName~TaskRunnerTest"`
Expected: FAIL (compile or verification mismatch).

- [ ] **Step 3: Implement pass-through**

In `TaskRunner.RunTask`, where `managerCallbackUrl` is derived, also derive the report path from the same job, then pass it:

```csharp
MergeJob? job = this._jobUtils.GetJob(task.JobId);
string? managerCallbackUrl = job?.Parameters.AdditionalParams?.JobTrackerServiceURL;
string? reportOutputPath = job?.Parameters.AdditionalParams?.ReportOutputPath;
```

(There is currently a single inline `this._jobUtils.GetJob(task.JobId)?...` call for `managerCallbackUrl`; replace it with the `job` local above so the job is fetched once.) Then:

```csharp
this._taskExecutor.ExecuteTask(task, this._taskUtils, managerCallbackUrl, reportOutputPath);
```

Add `using MergerService.Models.Jobs;` if not already present.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --filter "ClassName~TaskRunnerTest"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add MergerService/Runners/TaskRunner.cs MergerServiceUnitTests/Runners/TaskRunnerTest.cs
git commit -m "feat: pass job ReportOutputPath from TaskRunner to TaskExecutor"
```

---

## Task 8: Register `IReportWriter` + add `REPORT` config

**Files:**
- Modify: `MergerService/Program.cs`
- Modify: `MergerService/appsettings.json`

- [ ] **Step 1: Register the writer**

In `Program.cs`, after `builder.Services.AddSingleton<ITaskExecutor, TaskExecutor>();` add:

```csharp
builder.Services.AddSingleton<IReportWriter, ReportWriter>();
```

Add `using MergerService.Utils;` if not already imported. `IFileSystem`, `IAmazonS3`, and `IConfigurationManager` are already registered via `RegisterMergerLogicType()`.

- [ ] **Step 2: Add config section**

In `appsettings.json`, add a top-level `REPORT` section:

```json
  "REPORT": {
    "sink": "FS"
  },
```

- [ ] **Step 3: Build**

Run: `dotnet build GpkgMerger.sln`
Expected: succeeds.

- [ ] **Step 4: Commit**

```bash
git add MergerService/Program.cs MergerService/appsettings.json
git commit -m "build: register ReportWriter and add REPORT sink config"
```

---

## Task 9: Full build + test sweep

**Files:** none.

- [ ] **Step 1: Build the solution**

Run: `dotnet build GpkgMerger.sln`
Expected: succeeds with no errors.

- [ ] **Step 2: Run the full unit-test suite**

Run: `dotnet test GpkgMerger.sln --filter "TestCategory=unit"`
Expected: all tests pass, including the pre-existing `TileMergerTest`, `TaskExecutorTest`, and `TaskRunnerTest`.

- [ ] **Step 3: Verify the CLI still compiles against `ITileMerger`**

`MergerCli/Process.cs` uses the original `MergeTiles(...)` overload, which is preserved. Confirm it builds (covered by Step 1). No change required.

- [ ] **Step 4: Post the open question on the PR**

After opening the implementation PR, post the error-handling open question from the spec (`## Error handling — OPEN QUESTION`) as a PR comment so reviewers decide whether report-write failure should stay best-effort or fail the task.

---

## Post-implementation

- Follow-up ticket **MAPCO-11688** covers exposing these counts as Prometheus metrics + dashboard and auditing/reorganizing existing metrics/dashboards. Not part of this plan.
