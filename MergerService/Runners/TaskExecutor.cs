using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using MergerLogic.Utils;
using MergerService.Controllers;
using MergerService.Models.Tasks;
using MergerService.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;

namespace MergerService.Runners
{
    public class TaskExecutor : ITaskExecutor
    {
        private readonly IDataFactory _dataFactory;
        private readonly ITileMerger _tileMerger;
        private readonly ITimeUtils _timeUtils;
        private readonly ILogger _logger;
        private readonly ActivitySource _activitySource;
        private readonly IFileSystem _fileSystem;
        private readonly IMetricsProvider _metricsProvider;
        private readonly string _inputPath;
        private readonly string _gpkgPath;
        private readonly bool _limitBatchSize;
        private readonly int _batchMaxSize;
        private readonly long _batchMaxBytes;
        private readonly string _filePath;
        private readonly bool _shouldValidate;
        private readonly int _maxDegreeOfParallelism;
        private static readonly int DEFAULT_BATCH_SIZE = 1000;

        public TaskExecutor(IDataFactory dataFactory, ITileMerger tileMerger, ITimeUtils timeUtils, IConfigurationManager configurationManager,
            ILogger<TaskExecutor> logger, ActivitySource activitySource,
            IFileSystem fileSystem, IMetricsProvider metricsProvider)
        {
            this._dataFactory = dataFactory;
            this._tileMerger = tileMerger;
            this._timeUtils = timeUtils;
            this._logger = logger;
            this._activitySource = activitySource;
            this._fileSystem = fileSystem;
            this._metricsProvider = metricsProvider;
            this._inputPath = configurationManager.GetConfiguration("GENERAL", "inputPath");
            this._gpkgPath = configurationManager.GetConfiguration("GENERAL", "gpkgPath");
            this._filePath = configurationManager.GetConfiguration("GENERAL", "filePath");
            this._shouldValidate = configurationManager.GetConfiguration<bool>("GENERAL", "validate");
            this._batchMaxBytes = configurationManager.GetConfiguration<long>("GENERAL", "batchMaxBytes");
            this._batchMaxSize = configurationManager.GetConfiguration<int>("GENERAL", "batchSize", "batchMaxSize");
            this._limitBatchSize = configurationManager.GetConfiguration<bool>("GENERAL", "batchSize", "limitBatchSize");

            if (!this._limitBatchSize || this._batchMaxSize <= 0)
            {
                if (this._limitBatchSize && this._batchMaxSize <= 0)
                {
                    string methodName = MethodBase.GetCurrentMethod().Name;
                    this._logger.LogWarning($"[{methodName}] Got invalid max batch size: {this._batchMaxSize}, using default value: {DEFAULT_BATCH_SIZE}");
                }

                this._batchMaxSize = DEFAULT_BATCH_SIZE;
            }

            int numOfThreads = configurationManager.GetConfiguration<int>("GENERAL", "parallel", "numOfThreads");
            // 0 (unset) or negative means "let the runtime decide" per available cores.
            this._maxDegreeOfParallelism = numOfThreads > 0 ? numOfThreads : Environment.ProcessorCount;
        }

        public void ExecuteTask(MergeTask task, ITaskUtils taskUtils, string? managerCallbackUrl)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            this._logger.LogDebug($"[{methodName}] start {task.ToString()}");
            // Guard clause in case there are no batches or sources
            if (task.Parameters is null || task.Parameters.Batches is null || task.Parameters.Sources is null)
            {
                this._logger.LogWarning($"[{methodName}] Task parameters are invalid. Task: {task.ToString()}");

                try
                {
                    taskUtils.UpdateReject(task.JobId, task.Id, task.Attempts, "Task parameters are invalid", false, managerCallbackUrl);
                }
                catch (Exception e)
                {
                    this._logger.LogError(e, $"[{methodName}] Error in MergerService run - update task failure on invalid parameters: {e.Message}");
                }

                return;
            }

            MergeMetadata metadata = task.Parameters;
            Stopwatch mergeRunTimeStopwatch = new Stopwatch();
            TimeSpan ts;

            TileFormatStrategy strategy = new TileFormatStrategy(metadata.TargetFormat, metadata.OutputFormatStrategy);
            bool shouldUpscale = !metadata.IsNewTarget;

            // Log the task
            this._logger.LogInformation($"[{methodName}] starting task: {task.ToString()}");

            using (var taskActivity = this._activitySource.StartActivity("task processing"))
            {
                //TODO: add task identifier to activity
                //taskActivity.AddTag("jobId", task.jodId);
                //taskActivity.AddTag("taskId", task.id);

                this._logger.LogDebug($"[{methodName}] BuildDataList");
                List<IData> sources = this.BuildDataList(metadata.Sources, this._batchMaxSize);
                try
                {
                IData target = sources[0];
                target.IsNew = metadata.IsNewTarget;

                this._logger.LogDebug($"[{methodName}] Recived {metadata.Batches.Length} Batches");
                long totalTileCount = metadata.Batches.Sum(batch => batch.Size());
                long overallTileProgressCount = 0;
                int batchesCount = 0;
                this._logger.LogInformation($"[{methodName}] Total amount of tiles to merge: {totalTileCount}");
                foreach (TileBounds bounds in metadata.Batches)
                {
                    batchesCount++;
                    this._logger.LogDebug($"[{methodName}] Run on {batchesCount} batch: {bounds.ToString()}");
                    using (var batchActivity = this._activitySource.StartActivity("batch processing"))
                    {
                        // TODO: remove comment and check that the activity is created (When bug will be fixed)
                        // batchActivity.AddTag("bbox", bounds.ToString());
                        mergeRunTimeStopwatch.Reset();
                        mergeRunTimeStopwatch.Start();

                        long singleTileBatchCount = bounds.Size();
                        this._metricsProvider.TilesInBatchGauge(singleTileBatchCount);
                        // TODO: remove comment and check that the activity is created (When bug will be fixed)
                        // batchActivity.AddTag("size", totalTileCount);

                        // Skip if there are no tiles in the given bounds
                        if (singleTileBatchCount == 0)
                        {
                            continue;
                        }

                        this._logger.LogInformation($"[{methodName}] Total amount of tiles to merge for current batch: {singleTileBatchCount}");

                        // Go over the bounds of the current batch
                        using (this._activitySource.StartActivity($"[{methodName}] merging tiles"))
                        {
                            var batchWorkTimeStopwatch = Stopwatch.StartNew();

                            // Enumerate the coords once, then merge in parallel one chunk at a time. Each chunk
                            // is flushed to the target before the next starts, so memory stays bounded to ~one
                            // chunk (count-based; the previous byte cap is approximated by batchMaxSize).
                            var coords = new List<Coord>((int)singleTileBatchCount);
                            for (int x = bounds.MinX; x <= bounds.MaxX; x++)
                            {
                                for (int y = bounds.MinY; y <= bounds.MaxY; y++)
                                {
                                    coords.Add(new Coord(bounds.Zoom, x, y));
                                }
                            }

                            int chunkSize = this._batchMaxSize;
                            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = this._maxDegreeOfParallelism };

                            foreach (Coord[] chunk in coords.Chunk(chunkSize))
                            {
                                var mergedTiles = new ConcurrentBag<Tile>();

                                // Merge is the hot path and each coord is independent; the target is only read
                                // here (writes happen after this parallel phase), and GetCorrespondingTile is
                                // thread-safe per data source.
                                Parallel.ForEach(chunk, parallelOptions, coord =>
                                {
                                    var correspondingTileBuilders = new List<CorrespondingTileBuilder>
                                    {
                                        () => sources[0].GetCorrespondingTile(coord, shouldUpscale)
                                    };
                                    foreach (IData source in sources.Skip(1))
                                    {
                                        // TODO: upscale = false - this is a temporary fix till we decide how sources should be upscaled
                                        correspondingTileBuilders.Add(() => source.GetCorrespondingTile(coord, false));
                                    }

                                    var tileMergeStopwatch = Stopwatch.StartNew();
                                    Tile? tile = this._tileMerger.MergeTiles(correspondingTileBuilders, coord, strategy, metadata.IsNewTarget);
                                    tileMergeStopwatch.Stop();
                                    this._metricsProvider.MergeTimePerTileHistogram(tileMergeStopwatch.Elapsed.TotalSeconds, metadata.TargetFormat);

                                    if (tile != null)
                                    {
                                        mergedTiles.Add(tile);
                                    }
                                });

                                long progressAfterChunk = Interlocked.Add(ref overallTileProgressCount, chunk.Length);

                                if (!mergedTiles.IsEmpty)
                                {
                                    this.UpdateTargetTiles(target, mergedTiles.ToList(), task, progressAfterChunk, totalTileCount, taskUtils);
                                }

                                this._logger.LogInformation(
                                    $"[{methodName}] Job: {task.JobId}, Task: {task.Id}, Tile Count: {progressAfterChunk} / {totalTileCount}");
                                UpdateRelativeProgress(task, progressAfterChunk, totalTileCount, taskUtils);
                            }

                            batchWorkTimeStopwatch.Stop();
                            this._metricsProvider.BatchWorkTimeHistogram(batchWorkTimeStopwatch.Elapsed.TotalSeconds);
                        }

                        this._logger.LogInformation($"[{methodName}] Overall tile Count: {overallTileProgressCount} / {totalTileCount}");

                        mergeRunTimeStopwatch.Stop();

                        // Get the elapsed time as a TimeSpan value.
                        ts = mergeRunTimeStopwatch.Elapsed;
                        string elapsedMessage = this._timeUtils.FormatElapsedTime("Merge runtime: ", ts);
                        this._logger.LogInformation($"[{methodName}] Merged the following bounds: {bounds}. {elapsedMessage}");

                        // After merging, validate if requested
                        if (this._shouldValidate)
                        {
                            // Reset stopwatch for validation time measure
                            mergeRunTimeStopwatch.Reset();
                            mergeRunTimeStopwatch.Start();

                            this._logger.LogInformation($"[{methodName}] Validating merged data sources");
                            using (this._activitySource.StartActivity("validating tiles"))
                            {
                                bool valid = this.Validate(target, bounds);

                                if (!valid)
                                {
                                    try
                                    {
                                        taskUtils.UpdateReject(task.JobId, task.Id, task.Attempts, "Error in validation, target not valid after run", true, managerCallbackUrl);
                                    }
                                    catch (Exception e)
                                    {
                                        this._logger.LogError(e, $"[{methodName}] Error in MergerService run - update task failure after validation failure: {e.Message}");
                                    }
                                }
                            }
                            mergeRunTimeStopwatch.Stop();
                            this._metricsProvider.TotalValidationTimeHistogram(mergeRunTimeStopwatch.Elapsed.TotalSeconds);
                            // Get the elapsed time as a TimeSpan value.
                            ts = mergeRunTimeStopwatch.Elapsed;
                            string elapsedTime = this._timeUtils.FormatElapsedTime($"Validation time", ts);
                            this._logger.LogInformation(elapsedTime);
                        }
                        else
                        {
                            this._logger.LogInformation($"[{methodName}] Validation not requested, skipping validation...");
                        }
                    }
                    this._metricsProvider.TilesInBatchGauge(0);
                }
                target.Wrapup();
                }
                finally
                {
                    foreach (IData source in sources)
                    {
                        source.Dispose();
                    }
                }
            }
            this._logger.LogDebug($"[{methodName}] end");
        }

        private List<IData> BuildDataList(Source[] paths, int batchSize)
        {
            using (this._activitySource.StartActivity("sources parsing"))
            {
                var stopwatch = Stopwatch.StartNew();

                List<IData> sources = new List<IData>();

                if (paths.Length != 0)
                {
                    string path = BuildPath(paths[0], true);
                    sources.Add(this._dataFactory.CreateDataSource(paths[0].Type, path, batchSize, paths[0].Grid,
                        paths[0].Origin, paths[0].Extent, true));
                    foreach (Source source in paths.Skip(1))
                    {
                        // TODO: add support for HTTP
                        path = BuildPath(source, false);
                        sources.Add(this._dataFactory.CreateDataSource(source.Type, path, batchSize,
                            source.Grid, source.Origin));
                    }
                }
                stopwatch.Stop();
                this._metricsProvider.BuildSourcesListTime(stopwatch.Elapsed.TotalSeconds);
                return sources;
            }
        }

        private string BuildPath(Source source, bool isTarget)
        {
            string type = source.Type.ToUpper();
            string path = source.Path;

            // If the source is not the target
            if (!isTarget)
            {
                if (type == "S3")
                {
                    return path;
                }

                return this._fileSystem.Path.Join(this._inputPath, path);
            }

            if (type == "GPKG")
            {
                return this._fileSystem.Path.Join(this._gpkgPath, path);
            }

            if (type == "FS")
            {
                return this._fileSystem.Path.Join(this._filePath, path);
            }

            return path;
        }

        private void UpdateTargetTiles(IData target, List<Tile> tiles, MergeTask task, long overallTileProgressCount, long totalTileCount, ITaskUtils taskUtils)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;

            using (this._activitySource.StartActivity("saving tiles"))
            {
                this._logger.LogInformation($"[{methodName}] Updating {tiles.Count} tiles chunk Job: {task.JobId}, Task: {task.Id}");
                target.UpdateTiles(tiles);
                UpdateRelativeProgress(task, overallTileProgressCount, totalTileCount, taskUtils);
            }
        }

        private bool Validate(IData target, TileBounds bounds)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            int totalTileCount = (int)bounds.Size();
            int tilesChecked = 0;

            this._logger.LogInformation($"[{methodName}] If any missing tiles are found, they will be printed.");

            // Go over the bounds and check for missing tiles
            for (int x = bounds.MinX; x < bounds.MaxX; x++)
            {
                for (int y = bounds.MinY; y < bounds.MaxY; y++)
                {
                    Coord coord = new Coord(bounds.Zoom, x, y);
                    if (target.TileExists(coord))
                    {
                        ++tilesChecked;
                    }
                    else
                    {
                        this._logger.LogError($"[{methodName}] z: {bounds.Zoom}, x: {x}, y: {y}");
                    }

                    if (tilesChecked != 0 && tilesChecked % 1000 == 0)
                    {
                        tilesChecked += 1000;
                        this._logger.LogInformation($"[{methodName}] Total tiles checked: {tilesChecked}/{totalTileCount}");
                    }
                }
            }

            bool hasSameTiles = tilesChecked == totalTileCount;

            this._logger.LogInformation($"[{methodName}] Total tiles checked: {tilesChecked}/{totalTileCount}");
            this._logger.LogInformation($"[{methodName}] Target's valid: {hasSameTiles}");

            return hasSameTiles;
        }

        private void UpdateRelativeProgress(MergeTask task, long currentAmount, long totalAmount, ITaskUtils taskUtils)
        {
            using (var activity = this._activitySource.StartActivity("update relative task progress"))
            {
                try
                {
                    string methodName = MethodBase.GetCurrentMethod().Name;
                    this._logger.LogDebug($"[{methodName}] UpdateRelativeProgress");

                    task.Percentage = (int)(100 * (double)currentAmount / totalAmount);
                    UpdateParams updateParams = new UpdateParams()
                    {
                        Status = Status.IN_PROGRESS,
                        Percentage = task.Percentage
                    };
                    taskUtils.UpdateProgress(task.JobId, task.Id, updateParams);
                }
                catch (Exception e)
                {
                    this._logger.LogError(e, $"[{MethodBase.GetCurrentMethod().Name}] Error in MergerService run - update task percentage: {e.Message}");
                }
            }
        }
    }
}
