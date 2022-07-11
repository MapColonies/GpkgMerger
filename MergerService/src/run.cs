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

        public void Start()
        {
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts;

            this._logger.LogInformation("starting task polling loop");

            ITaskUtils taskUtils = new TaskUtils(this._configurationManager, this._requestUtils, this._taskUtilsLogger, this._activitySource);

            while (true)
            {
                MergeTask? task = null;

                try
                {
                    task = taskUtils.GetTask();
                }
                catch (Exception e)
                {
                    this._logger.LogError($"Error in MergerService run - get task: {e.Message}");
                }

                // Guard clause in case there are no batches or sources
                if (task == null || task.Batches == null || task.Sources == null)
                {
                    // Sleep for 0.5 sec so we won't send too many requests if there are no tasks available
                    // TODO: read from configuration
                    Thread.Sleep(1000);
                    continue;
                }

                // Log the task
                // TODO: add job and task ids
                this._logger.LogInformation($"starting task: taskId job: jobId");

                try
                {
                    using (var taskActivity = this._activitySource.StartActivity("task processing"))
                    {
                        //TODO: add task identifier to activity
                        //taskActivity.AddTag("jobId", task.jodId);
                        //taskActivity.AddTag("taskId", task.id);

                        foreach (TileBounds bounds in task.Batches)
                        {
                            Console.WriteLine($"activitySource: {this._activitySource}");
                            using (var batchActivity = this._activitySource.StartActivity("batch processing"))
                            {
                                // TODO: remove comment and check that the activity is created (When bug will be fixed)
                                // batchActivity.AddTag("bbox", bounds.ToString());

                                stopWatch.Reset();
                                stopWatch.Start();

                                int totalTileCount = bounds.Size();
                                int tileProgressCount = 0;

                                // TODO: remove comment and check that the activity is created (When bug will be fixed)
                                // batchActivity.AddTag("size", totalTileCount);

                                // Skip if there are no tiles in the given bounds
                                if (totalTileCount == 0)
                                {
                                    continue;
                                }

                                List<IData> sources = this.BuildDataList(task.Sources, totalTileCount);
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
                                this._logger.LogInformation($"Merged the following bounds: {bounds}. {elapsedMessage}");

                                // After merging, validate if requested
                                bool validate = this._configurationManager.GetConfiguration<bool>("GENERAL", "validate");
                                if (validate)
                                {
                                    // Reset stopwatch for validation time measure
                                    stopWatch.Reset();
                                    stopWatch.Start();

                                    this._logger.LogInformation("Validating merged data sources");
                                    using (this._activitySource.StartActivity("validating tiles"))
                                        this.Validate(target, bounds);

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
                }
            }
        }

        private void Validate(IData target, TileBounds bounds)
        {
            int totalTileCount = bounds.Size();
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

                    if (tilesChecked % 1000 == 0)
                    {
                        tilesChecked += 1000;
                        this._logger.LogInformation($"Total tiles checked: {tilesChecked}/{totalTileCount}");
                    }
                }
            }

            bool hasSameTiles = tilesChecked == totalTileCount;

            this._logger.LogInformation($"Total tiles checked: {tilesChecked}/{totalTileCount}");
            this._logger.LogInformation($"Target's valid: {hasSameTiles}");
        }
    }
}
