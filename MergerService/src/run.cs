using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProccessing;
using MergerLogic.Utils;
using MergerService.Controllers;
using System.Diagnostics;

namespace MergerService.Src
{
    public class Run : IRun
    {
        private IDataFactory _dataFactory;
        private ITileMerger _tileMerger;
        private ITimeUtils _timeUtils;
        private IConfigurationManager _configurationManager;

        public Run(IDataFactory dataFactory, ITileMerger tileMerger, ITimeUtils timeUtils, IConfigurationManager configurationManager)
        {
            this._dataFactory = dataFactory;
            this._tileMerger = tileMerger;
            this._timeUtils = timeUtils;
            this._configurationManager = configurationManager;
        }

        private List<IData> BuildDataList(Source[] paths, int batchSize)
        {
            List<IData> sources = new List<IData>();

            if (paths.Length != 0)
            {
                sources.Add(this._dataFactory.CreateDatasource(paths[0].Type, paths[0].Path, batchSize, true));
                foreach (Source source in paths.Skip(1))
                {
                    // TODO: add support for HTTP
                    sources.Add(this._dataFactory.CreateDatasource(source.Type, source.Path, batchSize, source.IsOneXOne(), source.Origin));
                }
            }

            return sources;
        }

        public void Start()
        {
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts;

            while (true)
            {
                MergeTask? task = null;

                try
                {
                    task = MergeTask.GetTask();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error in MergerService run - get task");
                    Console.WriteLine(e.Message);
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
                task.Print();

                try
                {
                    foreach (TileBounds bounds in task.Batches)
                    {
                        stopWatch.Reset();
                        stopWatch.Start();

                        int totalTileCount = bounds.Size();
                        int tileProgressCount = 0;

                        // Skip if there are no tiles in the given bounds
                        if (totalTileCount == 0)
                        {
                            continue;
                        }

                        List<IData> sources = this.BuildDataList(task.Sources, totalTileCount);
                        IData target = sources[0];

                        List<Tile> tiles = new List<Tile>(totalTileCount);

                        Console.WriteLine($"Total amount of tiles to merge: {totalTileCount}");

                        // Go over the bounds of the current batch
                        for (int x = bounds.MinX; x < bounds.MaxX; x++)
                        {
                            for (int y = bounds.MinY; y < bounds.MaxY; y++)
                            {
                                Coord coord = new Coord(bounds.Zoom, x, y);

                                // Create tile builder list for current coord for all sources
                                List<CorrespondingTileBuilder> correspondingTileBuilders = new List<CorrespondingTileBuilder>();
                                foreach (IData source in sources)
                                {
                                    correspondingTileBuilders.Add(() => source.GetCorrespondingTile(coord, true));
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
                                    Console.WriteLine($"Tile Count: {tileProgressCount} / {totalTileCount}");
                                }
                            }
                        }

                        target.UpdateTiles(tiles);

                        foreach (IData data in sources)
                        {
                            target.UpdateMetadata(data);
                        }

                        Console.WriteLine($"Tile Count: {tileProgressCount} / {totalTileCount}");
                        target.Wrapup();

                        stopWatch.Stop();

                        // Get the elapsed time as a TimeSpan value.
                        ts = stopWatch.Elapsed;
                        Console.WriteLine("Merged the following bounds:");
                        bounds.Print();
                        this._timeUtils.PrintElapsedTime("Merge runtime", ts);

                        // After merging, validate if requested
                        bool validate = bool.Parse(this._configurationManager.GetConfiguration("GENERAL", "validate"));
                        if (validate)
                        {
                            // Reset stopwatch for validation time measure
                            stopWatch.Reset();
                            stopWatch.Start();

                            Console.WriteLine("Validating merged datasources");
                            this.Validate(target, bounds);

                            stopWatch.Stop();
                            // Get the elapsed time as a TimeSpan value.
                            ts = stopWatch.Elapsed;
                            this._timeUtils.PrintElapsedTime($"Validation time", ts);
                        }
                        else
                        {
                            Console.WriteLine("Validation not requested, skipping validation...");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error in MergerService run - bounds loop");
                    Console.WriteLine(e.Message);
                }
            }
        }

        private void Validate(IData target, TileBounds bounds)
        {
            int totalTileCount = bounds.Size();
            int tilesChecked = 0;

            Console.WriteLine("If any missing tiles are found, they will be printed.");

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
                        Console.WriteLine($"z: {bounds.Zoom}, x: {x}, y: {y}");
                    }

                    if (tilesChecked % 1000 == 0)
                    {
                        tilesChecked += 1000;
                        Console.WriteLine($"Total tiles checked: {tilesChecked}/{totalTileCount}");
                    }
                }
            }

            bool hasSameTiles = tilesChecked == totalTileCount;

            Console.WriteLine($"Total tiles checked: {tilesChecked}/{totalTileCount}");
            Console.WriteLine($"Target's valid: {hasSameTiles}");
        }
    }
}
