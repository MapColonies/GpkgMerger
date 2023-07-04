using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using MergerService.Controllers;
using MergerService.Models.Tasks;
using MergerService.Utils;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Net;

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
        private readonly string _inputPath;
        private readonly string _gpkgPath;
        private readonly int _batchSize;
        private readonly string _filePath;
        private readonly bool _shouldValidate;

        public Run(IDataFactory dataFactory, ITileMerger tileMerger, ITimeUtils timeUtils, IConfigurationManager configurationManager,
            ILogger<Run> logger, ILogger<MergeTask> mergeTaskLogger, ILogger<TaskUtils> taskUtilsLogger, ILogger<JobUtils> jobUtilsLogger, ActivitySource activitySource,
            ITaskUtils taskUtils, IHttpRequestUtils requestUtils, IFileSystem fileSystem, IHeartbeatClient heartbeatClient)
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

            this._logger.LogInformation("starting task polling loop");
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
                            this._logger.LogDebug("No task was found to work on...");
                            continue;
                        }

                        this._logger.LogError($"Error in MergerService start - get task: {e.Message}");
                        continue;
                    }

                    // Guard clause in case there are no batches or sources
                    if (task == null)
                    {
                        continue;
                    }

                    string? managerCallbackUrl = jobUtils.GetJob(task.JobId)?.Parameters.ManagerCallbackUrl;
                    string log = managerCallbackUrl == null ? "managerCallbackUrl not provided as job parameter" : $"managerCallback url: {managerCallbackUrl}";
                    this._logger.LogDebug(log);

                    try
                    {
                        this._heartbeatClient.Start(task.Id);
                        RunTask(task, taskUtils, managerCallbackUrl);
                    }
                    catch (Exception e)
                    {
                        this._logger.LogError($"Error in MergerService while running task {task.Id}, error: {e.Message}");

                        try
                        {
                            taskUtils.UpdateReject(task.JobId, task.Id, task.Attempts, e.Message, true, managerCallbackUrl);
                        }
                        catch (Exception innerError)
                        {
                            this._logger.LogError(
                                $"Error in MergerService while updating reject status, RunTask catch block - update task failure: {innerError.Message}");
                        }

                        continue;
                    }
                    finally
                    {
                        this._heartbeatClient.Stop();
                    }

                    activatedAny = true;

                    try
                    {
                        taskUtils.UpdateCompletion(task.JobId, task.Id, managerCallbackUrl);
                        this._logger.LogInformation($"Completed task: jobId: {task.JobId}, taskId: {task.Id}");
                    }
                    catch (Exception e)
                    {
                        this._logger.LogError($"Error in MergerService start - update task completion: {e.Message}");
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
            // Guard clause in case there are no batches or sources
            if (task.Parameters is null || task.Parameters.Batches is null || task.Parameters.Sources is null)
            {
                this._logger.LogWarning($"Task parameters are invalid. Task: {task.ToString()}");

                try
                {
                    taskUtils.UpdateReject(task.JobId, task.Id, task.Attempts, "Task parameters are invalid", false, managerCallbackUrl);
                }
                catch (Exception e)
                {
                    this._logger.LogError(
                        $"Error in MergerService run - update task failure on invalid parameters: {e.Message}");
                }

                return;
            }

            MergeMetadata metadata = task.Parameters;
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts;

            bool shouldUpscale = !metadata.IsNewTarget;
            Func<IData, Coord, Tile?> getTileByCoord = metadata.IsNewTarget
                ? (_, _) => null
                : (source, coord) => source.GetCorrespondingTile(coord, shouldUpscale);

            // Log the task
            this._logger.LogInformation($"starting task: {task.ToString()}");

            using (var taskActivity = this._activitySource.StartActivity("task processing"))
            {
                //TODO: add task identifier to activity
                //taskActivity.AddTag("jobId", task.jodId);
                //taskActivity.AddTag("taskId", task.id);

                long totalTileCount = metadata.Batches.Sum(batch => batch.Size());
                long overallTileProgressCount = 0;
                this._logger.LogInformation($"Total amount of tiles to merge: {totalTileCount}");

                foreach (TileBounds bounds in metadata.Batches)
                {
                    using (var batchActivity = this._activitySource.StartActivity("batch processing"))
                    {
                        // TODO: remove comment and check that the activity is created (When bug will be fixed)
                        // batchActivity.AddTag("bbox", bounds.ToString());

                        stopWatch.Reset();
                        stopWatch.Start();

                        long singleTileBatchCount = bounds.Size();
                        int tileProgressCount = 0;

                        // TODO: remove comment and check that the activity is created (When bug will be fixed)
                        // batchActivity.AddTag("size", totalTileCount);

                        // Skip if there are no tiles in the given bounds
                        if (singleTileBatchCount == 0)
                        {
                            continue;
                        }

                        List<IData> sources = this.BuildDataList(metadata.Sources, this._batchSize);
                        IData target = sources[0];
                        target.IsNew = metadata.IsNewTarget;

                        // TODO: fix to use inner batch size (add iteration inside loop below)
                        List<Tile> tiles = new List<Tile>((int)singleTileBatchCount);

                        this._logger.LogInformation($"Total amount of tiles to merge for current batch: {singleTileBatchCount}");

                        // Go over the bounds of the current batch
                        using (this._activitySource.StartActivity("merging tiles"))
                        {
                            for (int x = bounds.MinX; x < bounds.MaxX; x++)
                            {
                                for (int y = bounds.MinY; y < bounds.MaxY; y++)
                                {
                                    Coord coord = new Coord(bounds.Zoom, x, y);

                                    // Create tile builder list for current coord for all sources
                                    List<CorrespondingTileBuilder> correspondingTileBuilders =
                                        new List<CorrespondingTileBuilder>();
                                    // Add target tile
                                    correspondingTileBuilders.Add(() => getTileByCoord(sources[0], coord));
                                    // Add all sources tiles 
                                    foreach (IData source in sources.Skip(1))
                                    {
                                        Tile? tile = source.GetCorrespondingTile(coord, shouldUpscale);
                                        correspondingTileBuilders.Add(() => tile);
                                    }

                                    byte[]? blob = this._tileMerger.MergeTiles(correspondingTileBuilders, coord,
                                        metadata.TargetFormat);

                                    if (blob != null)
                                    {
                                        tiles.Add(new Tile(coord, blob));
                                    }

                                    tileProgressCount++;
                                    overallTileProgressCount++;

                                    // Show progress every batchSize
                                    if (overallTileProgressCount % this._batchSize == 0)
                                    {
                                        this._logger.LogDebug(
                                            $"Job: {task.JobId}, Task: {task.Id}, Tile Count: {overallTileProgressCount} / {totalTileCount}");
                                            UpdateRelativeProgress(task, overallTileProgressCount, totalTileCount, taskUtils);
                                    }
                                }
                            }
                        }

                        using (this._activitySource.StartActivity("saving tiles"))
                        {
                            target.UpdateTiles(tiles);
                            UpdateRelativeProgress(task, overallTileProgressCount, totalTileCount, taskUtils);
                        }

                        this._logger.LogInformation($"Tile Count: {overallTileProgressCount} / {totalTileCount}");
                        target.Wrapup();

                        stopWatch.Stop();

                        // Get the elapsed time as a TimeSpan value.
                        ts = stopWatch.Elapsed;
                        string elapsedMessage = this._timeUtils.FormatElapsedTime("Merge runtime: ", ts);
                        this._logger.LogInformation($"Merged the following bounds: {bounds}. {elapsedMessage}");

                        // After merging, validate if requested
                        if (this._shouldValidate)
                        {
                            // Reset stopwatch for validation time measure
                            stopWatch.Reset();
                            stopWatch.Start();

                            this._logger.LogInformation("Validating merged data sources");
                            using (this._activitySource.StartActivity("validating tiles"))
                            {
                                bool valid = this.Validate(target, bounds);

                                if (!valid)
                                {
                                    try
                                    {
                                        taskUtils.UpdateReject(task.JobId, task.Id, task.Attempts,
                                            "Error in validation, target not valid after run", false, managerCallbackUrl);
                                    }
                                    catch (Exception innerError)
                                    {
                                        this._logger.LogError(
                                            $"Error in MergerService run - update task failure after validation failure: {innerError.Message}");
                                    }
                                }
                            }

                            stopWatch.Stop();
                            // Get the elapsed time as a TimeSpan value.
                            ts = stopWatch.Elapsed;
                            string elapsedTime = this._timeUtils.FormatElapsedTime($"Validation time", ts);
                            this._logger.LogInformation(elapsedTime);
                        }
                        else
                        {
                            this._logger.LogInformation("Validation not requested, skipping validation...");
                        }
                    }
                }
            }
        }

        private bool Validate(IData target, TileBounds bounds)
        {
            int totalTileCount = (int)bounds.Size();
            int tilesChecked = 0;

            this._logger.LogInformation("If any missing tiles are found, they will be printed.");

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
                        this._logger.LogError($"z: {bounds.Zoom}, x: {x}, y: {y}");
                    }

                    if (tilesChecked != 0 && tilesChecked % 1000 == 0)
                    {
                        tilesChecked += 1000;
                        this._logger.LogInformation($"Total tiles checked: {tilesChecked}/{totalTileCount}");
                    }
                }
            }

            bool hasSameTiles = tilesChecked == totalTileCount;

            this._logger.LogInformation($"Total tiles checked: {tilesChecked}/{totalTileCount}");
            this._logger.LogInformation($"Target's valid: {hasSameTiles}");

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
                    this._logger.LogError(
                        $"Error in MergerService run - update task percentage: {e.Message}");
                }
            }
        }
    }
}
