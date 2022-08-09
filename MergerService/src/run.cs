using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using MergerService.Controllers;
using MergerService.Utils;
using System.Diagnostics;

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
        private readonly ActivitySource _activitySource;
        private readonly ITaskUtils _taskUtils;
        private readonly IHttpRequestUtils _requestUtils;

        public Run(IDataFactory dataFactory, ITileMerger tileMerger, ITimeUtils timeUtils, IConfigurationManager configurationManager,
            ILogger<Run> logger, ILogger<MergeTask> mergeTaskLogger, ILogger<TaskUtils> taskUtilsLogger, ActivitySource activitySource,
            ITaskUtils taskUtils, IHttpRequestUtils requestUtils)
        {
            this._dataFactory = dataFactory;
            this._tileMerger = tileMerger;
            this._timeUtils = timeUtils;
            this._configurationManager = configurationManager;
            this._logger = logger;
            this._activitySource = activitySource;
            this._mergeTaskLogger = mergeTaskLogger;
            this._taskUtilsLogger = taskUtilsLogger;
            this._taskUtils = taskUtils;
            this._requestUtils = requestUtils;
        }

        private List<IData> BuildDataList(Source[] paths, int batchSize)
        {
            using (this._activitySource.StartActivity("sources parsing"))
            {
                List<IData> sources = new List<IData>();

                if (paths.Length != 0)
                {
                    //TODO: add extent
                    sources.Add(this._dataFactory.CreateDataSource(paths[0].Type, paths[0].Path, batchSize, paths[0].IsOneXOne(), paths[0].Origin, null, true));
                    foreach (Source source in paths.Skip(1))
                    {
                        // TODO: add support for HTTP
                        sources.Add(this._dataFactory.CreateDataSource(source.Type, source.Path, batchSize,
                            source.IsOneXOne(), source.Origin));
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
            ITaskUtils taskUtils = new TaskUtils(this._configurationManager, this._requestUtils, this._taskUtilsLogger, this._activitySource);

            this._logger.LogInformation("starting task polling loop");
            while (true)
            {
                MergeTask? task = null;
                bool activatedAny = false;

                foreach (var item in taskTypes)
                {
                    string jobType = item.Key;
                    string taskType = item.Value;

                    try
                    {
                        task = taskUtils.GetTask(jobType, taskType);
                    }
                    catch (Exception e)
                    {
                        this._logger.LogError($"Error in MergerService start - get task: {e.Message}");
                    }

                    // Guard clause in case there are no batches or sources
                    if (task == null)
                    {
                        continue;
                    }

                    RunTask(task, taskUtils);
                    activatedAny = true;

                    try
                    {
                        taskUtils.UpdateCompletion(task.JobId, task.Id);
                        taskUtils.NotifyOnCompletion(task.JobId, task.Id);
                    }
                    catch (Exception e)
                    {
                        this._logger.LogError($"Error in MergerService start - update task: {e.Message}");
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

        private void RunTask(MergeTask task, ITaskUtils taskUtils)
        {
            // Guard clause in case there are no batches or sources
            if (task.Parameters is null || task.Parameters.Batches is null || task.Parameters.Sources is null)
            {
                this._logger.LogWarning($"Task parameters are invalid. Task: {task.ToString()}");
                return;
            }

            MergeMetadata metadata = task.Parameters;
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts;

            // Log the task
            this._logger.LogInformation($"starting task: {task.Id} job: {task.JobId}");

            try
            {
                using (var taskActivity = this._activitySource.StartActivity("task processing"))
                {
                    //TODO: add task identifier to activity
                    //taskActivity.AddTag("jobId", task.jodId);
                    //taskActivity.AddTag("taskId", task.id);

                    foreach (TileBounds bounds in metadata.Batches)
                    {
                        using (var batchActivity = this._activitySource.StartActivity("batch processing"))
                        {
                            // TODO: remove comment and check that the activity is created (When bug will be fixed)
                            // batchActivity.AddTag("bbox", bounds.ToString());

                            stopWatch.Reset();
                            stopWatch.Start();

                            int totalTileCount = (int)bounds.Size();
                            int tileProgressCount = 0;

                            // TODO: remove comment and check that the activity is created (When bug will be fixed)
                            // batchActivity.AddTag("size", totalTileCount);

                            // Skip if there are no tiles in the given bounds
                            if (totalTileCount == 0)
                            {
                                continue;
                            }

                            List<IData> sources = this.BuildDataList(metadata.Sources, totalTileCount);
                            IData target = sources[0];

                            List<Tile> tiles = new List<Tile>(totalTileCount);

                            this._logger.LogInformation($"Total amount of tiles to merge: {totalTileCount}");

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
                                        foreach (IData source in sources)
                                        {
                                            correspondingTileBuilders.Add(
                                                () => source.GetCorrespondingTile(coord, true));
                                        }

                                        byte[]? blob = this._tileMerger.MergeTiles(correspondingTileBuilders, coord);

                                        if (blob != null)
                                        {
                                            tiles.Add(new Tile(coord, blob));
                                        }

                                        tileProgressCount++;

                                        // Show progress every 1000 tiles
                                        if (tileProgressCount % 1000 == 0)
                                        {
                                            this._logger.LogInformation($"Tile Count: {tileProgressCount} / {totalTileCount}");

                                            try
                                            {
                                                task.Percentage = 100 * (tileProgressCount / totalTileCount);
                                                taskUtils.UpdateProgress(task.JobId, task.Id, task.Percentage);
                                            }
                                            catch (Exception e)
                                            {
                                                this._logger.LogError($"Error in MergerService run - update task: {e.Message}");
                                            }
                                        }
                                    }
                                }
                            }

                            using (this._activitySource.StartActivity("saving tiles"))
                            {
                                target.UpdateTiles(tiles);
                            }

                            this._logger.LogInformation($"Tile Count: {tileProgressCount} / {totalTileCount}");
                            target.Wrapup();

                            stopWatch.Stop();

                            // Get the elapsed time as a TimeSpan value.
                            ts = stopWatch.Elapsed;
                            string elapsedMessage = this._timeUtils.FormatElapsedTime("Merge runtime: ", ts);
                            this._logger.LogInformation($"Merged the following bounds: {bounds} for zoom: {bounds.Zoom}. {elapsedMessage}");

                            // After merging, validate if requested
                            bool validate = this._configurationManager.GetConfiguration<bool>("GENERAL", "validate");
                            if (validate)
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
                                            taskUtils.UpdateReject(task.JobId, task.Id, task.Attempts, "Error in validation, target not valid after run", true);
                                        }
                                        catch (Exception innerError)
                                        {
                                            this._logger.LogError($"Error in MergerService run - update task failure after validation failure: {innerError.Message}");
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
            catch (Exception e)
            {
                this._logger.LogError("Error in MergerService run - bounds loop");
                this._logger.LogError(e.Message);

                try
                {
                    taskUtils.UpdateFailed(task.JobId, task.Id, e.Message);
                }
                catch (Exception innerError)
                {
                    this._logger.LogError($"Error in MergerService run catch block - update task failure: {innerError.Message}");
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
    }
}
