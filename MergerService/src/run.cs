using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProccessing;
using MergerLogic.Utils;
using MergerService.Controllers;
using System.Diagnostics;

namespace MergerService.Src
{
    public class Run
    {
        private static List<Data> BuildDataList(Source[] paths, int batchSize)
        {
            List<Data> sources = new List<Data>();

            if (paths.Length != 0)
            {
                sources.Add(Data.CreateDatasource(paths[0].Type, paths[0].Path, batchSize, true));
                foreach (Source source in paths.Skip(1))
                {
                    sources.Add(Data.CreateDatasource(source.Type, source.Path, batchSize, source.IsOneXOne()));
                }
            }

            return sources;
        }

        public static void Start()
        {
            Stopwatch stopWatch = new Stopwatch();
            TimeSpan ts;

            while (true)
            {
                // Sleep for 0.5 sec so we won't send too many requests if there are no tasks available
                Thread.Sleep(500);

                MergeTask? task = MergeTask.GetTask();

                // Guard clause in case there are no batches or sources
                if (task == null || task.Batches == null || task.Sources == null)
                {
                    continue;
                }

                foreach (Bounds bounds in task.Batches)
                {
                    stopWatch.Reset();
                    stopWatch.Start();

                    int totalTileCount = bounds.Size();
                    int tileProgressCount = 0;

                    List<Data> sources = BuildDataList(task.Sources, totalTileCount);
                    Data target = sources[0];

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
                            foreach (Data source in sources)
                            {
                                correspondingTileBuilders.Add(() => source.GetCorrespondingTile(coord, true));
                            }

                            byte[]? blob = Merge.MergeTiles(correspondingTileBuilders, coord);

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

                    foreach (Data data in sources)
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
                    TimeUtils.PrintElapsedTime("Merge runtime", ts);

                    // After merging, validate if requested
                    bool validate = bool.Parse(MergerLogic.Utils.Configuration.Instance.GetConfiguration("GENERAL", "validate"));
                    if (validate)
                    {
                        // Reset stopwatch for validation time measure
                        stopWatch.Reset();
                        stopWatch.Start();

                        Console.WriteLine("Validating merged datasources");
                        Validate(target, bounds);

                        stopWatch.Stop();
                        // Get the elapsed time as a TimeSpan value.
                        ts = stopWatch.Elapsed;
                        TimeUtils.PrintElapsedTime($"Validation time", ts);
                    }
                }
            }
        }

        private static void Validate(Data target, Bounds bounds)
        {
            int totalTileCount = bounds.Size();
            int tilesChecked = 0;

            Console.WriteLine("If any missing tiles are found, they will be printed.");

            // Go over the bounds and check for missing tiles
            for (int x = bounds.MinX; x < bounds.MaxX; x++)
            {
                for (int y = bounds.MinY; y < bounds.MaxY; y++)
                {
                    if (target.TileExists(bounds.Zoom, x, y))
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
