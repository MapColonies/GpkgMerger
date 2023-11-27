using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using MergerLogic.Utils;
using MergerService.Controllers;
using MergerService.Models.Tasks;
using MergerService.Utils;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Net;
using System.Reflection;

namespace MergerService.Src
{
    public class Run : IRun
    {
        private readonly IDataFactory _dataFactory;
        private readonly ITileMerger _tileMerger;
        private readonly ITimeUtils _timeUtils;
        private readonly IConfigurationManager _configurationManager;
        private readonly ILogger _logger;
        private readonly ILogger<MergeTask> _mergeTaskLogger;
        private readonly ILogger<TaskUtils> _taskUtilsLogger;
        private readonly ILogger<JobUtils> _jobUtilsLogger;
        private readonly ActivitySource _activitySource;
        private readonly ITaskUtils _taskUtils;
        private readonly IHttpRequestUtils _requestUtils;
        private readonly IFileSystem _fileSystem;
        private readonly IHeartbeatClient _heartbeatClient;
        private readonly IMetricsProvider _metricsProvider;
        private readonly string _inputPath;
        private readonly string _gpkgPath;
        private readonly int _batchSize;
        private readonly string _filePath;
        private readonly bool _shouldValidate;

        public Run(IDataFactory dataFactory, ITileMerger tileMerger, ITimeUtils timeUtils, IConfigurationManager configurationManager,
            ILogger<Run> logger, ILogger<MergeTask> mergeTaskLogger, ILogger<TaskUtils> taskUtilsLogger, ILogger<JobUtils> jobUtilsLogger, ActivitySource activitySource,
            ITaskUtils taskUtils, IHttpRequestUtils requestUtils, IFileSystem fileSystem, IHeartbeatClient heartbeatClient, IMetricsProvider metricsProvider)
        {
            this._dataFactory = dataFactory;
            this._tileMerger = tileMerger;
            this._timeUtils = timeUtils;
            this._configurationManager = configurationManager;
            this._logger = logger;
            this._activitySource = activitySource;
            this._mergeTaskLogger = mergeTaskLogger;
            this._taskUtilsLogger = taskUtilsLogger;
            this._jobUtilsLogger = jobUtilsLogger;
            this._taskUtils = taskUtils;
            this._requestUtils = requestUtils;
            this._fileSystem = fileSystem;
            this._heartbeatClient = heartbeatClient;
            this._metricsProvider = metricsProvider;
            this._inputPath = this._configurationManager.GetConfiguration("GENERAL", "inputPath");
            this._gpkgPath = this._configurationManager.GetConfiguration("GENERAL", "gpkgPath");
            this._filePath = this._configurationManager.GetConfiguration("GENERAL", "filePath");
            this._shouldValidate = this._configurationManager.GetConfiguration<bool>("GENERAL", "validate");
            this._batchSize = this._configurationManager.GetConfiguration<int>("GENERAL", "batchSize");
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

        private List<KeyValuePair<string, string>> BuildTypeList()
        {
            var taskTypes = this._configurationManager.GetChildren("TASK", "types");

            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();

            foreach (var pair in taskTypes)
            {
                var jobType = pair.GetValue<string>("JobType");
                var taskType = pair.GetValue<string>("taskType");
                values.Add(new KeyValuePair<string, string>(jobType, taskType));
            }

            return values;
        }

        public void Start()
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            this._logger.LogDebug($"[{methodName}] Start App");
            var pollingTime = this._configurationManager.GetConfiguration<int>("TASK", "pollingTime");

            var taskTypes = BuildTypeList();
            if (taskTypes.Count == 0)
            {
                string message = "No tasks configured, please provide job and task types";
                this._logger.LogCritical(message);
                throw new Exception(message);
            }

            ITaskUtils taskUtils = new TaskUtils(this._configurationManager, this._requestUtils, this._taskUtilsLogger, this._activitySource, this._heartbeatClient);
            IJobUtils jobUtils = new JobUtils(this._configurationManager, this._requestUtils, this._jobUtilsLogger, this._activitySource, this._heartbeatClient);

            this._logger.LogInformation($"[{methodName}] starting task polling loop");
            while (true)
            {
                bool activatedAny = false;
                foreach (var item in taskTypes)
                {
                    MergeTask? task = null;
                    string jobType = item.Key;
                    string taskType = item.Value;

                    try
                    {
                        task = taskUtils.GetTask(jobType, taskType);
                    }
                    catch (Exception e)
                    {
                        if (e is HttpRequestException &&
                            ((HttpRequestException)e).StatusCode == HttpStatusCode.NotFound)
                        {
                            this._logger.LogDebug($"[{methodName}] No task was found to work on...");
                            continue;
                        }

                        this._logger.LogError(e, $"[{methodName}] Error in MergerService start - get task: {e.Message}");
                        continue;
                    }
                    // Guard clause in case there are no batches or sources
                    if (task == null)
                    {
                        continue;
                    }

                    string? managerCallbackUrl = jobUtils.GetJob(task.JobId)?.Parameters.ManagerCallbackUrl;
                    string log = managerCallbackUrl == null ? "managerCallbackUrl not provided as job parameter" : $"managerCallback url: {managerCallbackUrl}";
                    this._logger.LogDebug($"[{methodName}]{log}");

                    // fail task that was released by task liberator and reached max attempts
                    if (task.Attempts > taskUtils.MaxAttempts)
                    {
                        try
                        {
                            string reason = string.IsNullOrEmpty(task.Reason) ? "Max attempts reached" : $"{task.Reason} and Max attempts reached";
                            taskUtils.UpdateReject(task.JobId, task.Id, task.Attempts, reason, true, managerCallbackUrl);
                        }
                        catch (Exception innerError)
                        {
                            this._logger.LogError(innerError, $"[{methodName}] Error in MergerService while updating reject status, update task failure: {innerError.Message}");
                        }
                        continue;
                    }

                    var totalTaskStopwatch = Stopwatch.StartNew();
                    bool taskSucceed = false;

                    try
                    {
                        this._heartbeatClient.Start(task.Id);
                        RunTask(task, taskUtils, managerCallbackUrl);
                        taskSucceed = true;
                    }
                    catch (Exception e)
                    {
                        this._logger.LogError(e, $"[{methodName}] Error in MergerService while running task {task.Id}, error: {e.Message}");

                        try
                        {
                            taskUtils.UpdateReject(task.JobId, task.Id, task.Attempts, e.Message, true, managerCallbackUrl);
                        }
                        catch (Exception innerError)
                        {
                            this._logger.LogError(e, $"[{methodName}] Error in MergerService while updating reject status, RunTask catch block - update task failure: {innerError.Message}");
                        }
                    }
                    finally
                    {
                        totalTaskStopwatch.Stop();
                        this._metricsProvider.TaskExecutionTimeHistogram(totalTaskStopwatch.Elapsed.TotalSeconds, taskType);
                        this._heartbeatClient.Stop();
                    }

                    if (!taskSucceed)
                    {
                        continue;
                    }

                    activatedAny = true;

                    try
                    {
                        taskUtils.UpdateCompletion(task.JobId, task.Id, managerCallbackUrl);
                        this._logger.LogInformation($"[{methodName}] Completed task: jobId: {task.JobId}, taskId: {task.Id}");
                    }
                    catch (Exception e)
                    {
                        this._logger.LogError(e, $"[{methodName}] Error in MergerService start - update task completion: {e.Message}");
                    }
                }

                // Sleep only if there was no task to run for any type
                if (!activatedAny)
                {
                    // Sleep for 1 sec so we won't send too many requests if there are no tasks available
                    Thread.Sleep(pollingTime);
                }
            }
        }

        private void RunTask(MergeTask task, ITaskUtils taskUtils, string? managerCallbackUrl)
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

            bool shouldUpscale = !metadata.IsNewTarget;
            Func<IData, Coord, Tile?> getTileByCoord = metadata.IsNewTarget
                ? (_, _) => null
                : (source, coord) =>
                {
                    Tile? resultTile = source.GetCorrespondingTile(coord, shouldUpscale);
                    return resultTile;
                };

            // Log the task
            this._logger.LogInformation($"[{methodName}] starting task: {task.ToString()}");

            using (var taskActivity = this._activitySource.StartActivity("task processing"))
            {
                //TODO: add task identifier to activity
                //taskActivity.AddTag("jobId", task.jodId);
                //taskActivity.AddTag("taskId", task.id);

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
                        Stopwatch batchInitializationStopwatch = Stopwatch.StartNew();
                        mergeRunTimeStopwatch.Reset();
                        mergeRunTimeStopwatch.Start();

                        long singleTileBatchCount = bounds.Size();
                        this._metricsProvider.TilesInBatchGauge(singleTileBatchCount);
                        int tileProgressCount = 0;

                        // TODO: remove comment and check that the activity is created (When bug will be fixed)
                        // batchActivity.AddTag("size", totalTileCount);

                        // Skip if there are no tiles in the given bounds
                        if (singleTileBatchCount == 0)
                        {
                            continue;
                        }

                        this._logger.LogDebug($"[{methodName}] BuildDataList");
                        List<IData> sources = this.BuildDataList(metadata.Sources, this._batchSize);
                        batchInitializationStopwatch.Stop();
                        this._metricsProvider.BatchInitializationTimeHistogram(batchInitializationStopwatch.Elapsed.TotalSeconds);
                        IData target = sources[0];
                        target.IsNew = metadata.IsNewTarget;

                        // TODO: fix to use inner batch size (add iteration inside loop below)
                        List<Tile> tiles = new List<Tile>((int)singleTileBatchCount);

                        this._logger.LogInformation($"[{methodName}] Total amount of tiles to merge for current batch: {singleTileBatchCount}");

                        // Go over the bounds of the current batch
                        using (this._activitySource.StartActivity($"[{methodName}] merging tiles"))
                        {
                            var batchWorkTimeStopwatch = Stopwatch.StartNew();

                            for (int x = bounds.MinX; x < bounds.MaxX; x++)
                            {
                                for (int y = bounds.MinY; y < bounds.MaxY; y++)
                                {
                                    this._logger.LogDebug($"[{methodName}] Handle tile z:{bounds.Zoom}, x:{x}, y:{y}");
                                    Coord coord = new Coord(bounds.Zoom, x, y);

                                    // Create tile builder list for current coord for all sources
                                    List<CorrespondingTileBuilder> correspondingTileBuilders = new List<CorrespondingTileBuilder>();
                                    // Add target tile
                                    correspondingTileBuilders.Add(() => getTileByCoord(sources[0], coord));
                                    // Add all sources tiles 
                                    this._logger.LogDebug($"[{methodName}] Get tile sources");
                                    foreach (IData source in sources.Skip(1))
                                    {
                                        // TODO: upscale = false - this is a temporary fix till we decide how sources should be upscaled
                                        correspondingTileBuilders.Add(() => source.GetCorrespondingTile(coord, false));
                                    }
                                    var tileMergeStopwatch = Stopwatch.StartNew();
                                    byte[]? blob = this._tileMerger.MergeTiles(correspondingTileBuilders, coord,
                                        metadata.TargetFormat);
                                    tileMergeStopwatch.Stop();
                                    this._metricsProvider.MergeTimePerTileHistogram(tileMergeStopwatch.Elapsed.TotalSeconds, metadata.TargetFormat);

                                    if (blob != null)
                                    {
                                        tiles.Add(new Tile(coord, blob));
                                    }

                                    tileProgressCount++;
                                    overallTileProgressCount++;

                                    // Show progress every batchSize
                                    if (overallTileProgressCount % this._batchSize == 0)
                                    {
                                        this._logger.LogInformation(
                                            $"[{methodName}] Job: {task.JobId}, Task: {task.Id}, Tile Count: {overallTileProgressCount} / {totalTileCount}");
                                        UpdateRelativeProgress(task, overallTileProgressCount, totalTileCount, taskUtils);
                                    }
                                }
                            }
                            batchWorkTimeStopwatch.Stop();
                            this._metricsProvider.BatchWorkTimeHistogram(batchWorkTimeStopwatch.Elapsed.TotalSeconds);
                        }

                        using (this._activitySource.StartActivity("saving tiles"))
                        {
                            this._logger.LogInformation($"[{methodName}] target UpdateTiles");
                            target.UpdateTiles(tiles);
                            this._logger.LogDebug($"[{methodName}] UpdateRelativeProgress");
                            UpdateRelativeProgress(task, overallTileProgressCount, totalTileCount, taskUtils);
                        }

                        this._logger.LogInformation($"[{methodName}] Overall tile Count: {overallTileProgressCount} / {totalTileCount}");
                        target.Wrapup();

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
            }
            this._logger.LogDebug($"[{methodName}] end");
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

        public void UpdateRelativeProgress(MergeTask task, long currentAmount, long totalAmount, ITaskUtils taskUtils)
        {
            using (var activity = this._activitySource.StartActivity("update relative task progress"))
            {
                try
                {
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
