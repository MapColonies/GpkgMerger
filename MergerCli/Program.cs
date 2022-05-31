﻿using MergerCli.Utils;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using System.Diagnostics;
using System.Runtime.Loader;

namespace MergerCli
{
    internal class Program
    {
        private static BatchStatusManager batchStatusManager;
        private static bool done = false;

        private static void Main(string[] args)
        {
            Stopwatch totalTimeStopWatch = new Stopwatch();
            totalTimeStopWatch.Start();
            TimeSpan ts;
            string programName = args[0];

            // Require input of wanted batch size and 2 types and paths (base and new gpkg)
            if (args.Length < 8 && args.Length != 2)
            {
                PrintHelp(programName);
                return;
            }

            PrepareStatusManger(ref args);

            int batchSize = int.Parse(args[1]);
            List<Data> sources = parseSources(args, batchSize);

            Data baseData = sources[0];
            if (sources.Count < 2)
            {
                Console.WriteLine("minimum of 2 sources is required");
                PrintHelp(programName);
                return;
            }

            try
            {
                bool validate = bool.Parse(Configuration.Instance.GetConfiguration("GENERAL", "validate"));
                for (int i = 1; i < sources.Count; i++)
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    if (batchStatusManager.IsLayerCompleted(sources[i].path))
                    {
                        continue;
                    }
                    Proccess.Start(baseData, sources[i], batchSize, batchStatusManager);
                    stopWatch.Stop();

                    // Get the elapsed time as a TimeSpan value.
                    ts = stopWatch.Elapsed;
                    TimeUtils.PrintElapsedTime($"{sources[i].path} merge runtime", ts);


                    if (validate)
                    {
                        // Reset stopwatch for validation time measure
                        stopWatch.Reset();
                        stopWatch.Start();

                        Console.WriteLine("Validating merged data sources");
                        Proccess.Validate(baseData, sources[i]);

                        stopWatch.Stop();
                        // Get the elapsed time as a TimeSpan value.
                        ts = stopWatch.Elapsed;
                        TimeUtils.PrintElapsedTime($"{sources[i].path} validation time", ts);
                    }
                }
            }
            catch (Exception ex)
            {
                //save status on unhandled exceptions
                OnFailure();
                Console.WriteLine(ex.ToString());
                return;
            }
            totalTimeStopWatch.Stop();
            // Get the elapsed time as a TimeSpan value.
            ts = totalTimeStopWatch.Elapsed;
            TimeUtils.PrintElapsedTime("Total runtime", ts);
            done = true;
        }

        private static void PrintHelp(string programName)
        {
            Console.WriteLine($@"Usage:

                                    Supported sources parameters:
                                        web sources (cant be base source):
                                            <'xyz' / 'wmts' / 'tms'> <url template> < is 1x1 > <bbox - in format 'minX,minY,maxX,maxY'> <min zoom> <max zoom> 
                                        file sources:
                                            <'fs' / 's3' / 'gpkg'> <path> < is 1x1 >
                                        **** please note all layers must be 2X1 EPSG:4326 layers ****
                                    
                                    merge sources: {programName} <batch_size> <base source> <addiotional source> [<another source source>...]
                                    Examples:
                                    {programName} 1000 gpkg area1.gpkg false gpkg area2.gpkg false
                                    {programName} 1000 s3 /path1/on/s3 false s3 /path2/on/s3 false
                                    {programName} 1000 s3 /path/on/s3 false gpkg geo.gpkg false
                                    {programName} 1000 s3 /path/on/s3 xyz http://xyzSourceUrl/{{z}}/{{x}}/{{y}}.png -180,-90,180,90 0 21
                                    {programName} 1000 gpkg geo.gpkg false gpkg 1x1.gpkg true gpkg area2.gpkg false gpkg area3.gpkg false
                                    {programName} 1000 gpkg geo.gpkg false s3 area1 false s3 area2 false s3 area3 false
                                    {programName} 1000 s3 geo false gpkg area1 false s3 1x1 true gpkg area3 false wmts http://wmtsSourceUrl/{{TileMatrix}}/{{TileCol}}/{{TileRow}}.png -180,-90,180,90 0 21 false

                                    Resume: {programName} <path to status file>.
                                    status file named 'status.json' will be created in running directory on incomplete merges.
                                    **** please note ***
                                    resuming partial merge with FS sources may result in incomplete data after merge as file order may not be consistent.
                                    FS file order should be consistent at least in most use cases (os and fs type combination) but is not guaranteed to be.
                                    Example:
                                    {programName} status.json
                                                             
                                    Minimal requirement is supplying at least one source.");
        }

        private static void PrepareStatusManger(ref string[] args)
        {
            if (args.Length == 2)
            {
                if (!File.Exists(args[1]))
                {
                    Console.WriteLine($"invalid status file {args[1]}");
                    Environment.Exit(-1);
                }
                string json = File.ReadAllText(args[1]);
                batchStatusManager = BatchStatusManager.FromJson(json);
                args = batchStatusManager.Command;
                Console.WriteLine("resuming layers merge operation. layers progress:");
                foreach (var item in batchStatusManager.States)
                {
                    Console.WriteLine($"{item.Key} {item.Value.BatchIdentifier}");
                }
            }
            else
            {
                batchStatusManager = new BatchStatusManager(args);
            }
            //save status on program exit
            AssemblyLoadContext.Default.Unloading += delegate { OnFailure(); };
            //save status on SigInt (ctrl + c)
            Console.CancelKeyPress += delegate { OnFailure(); };
        }

        private static void OnFailure()
        {
            if (!done)
            {
                string status = batchStatusManager.ToString();
                File.WriteAllText("status.json", status);
            }
            else
            {
                File.Delete("status.json");
            }
        }

        private static List<Data> parseSources(string[] args, int batchSize)
        {
            List<Data> sources = new List<Data>();
            int idx = 2;
            bool isBase = true;
            while(idx < args.Length)
            {
                switch (args[idx].ToLower())
                {
                    case "fs":
                    case "s3":
                    case "gpkg":
                        try
                        {
                            sources.Add(ParseFileSource(args, ref idx, batchSize, isBase));
                        } catch
                        {
                            string source = isBase ? "base" : "new";
                            Console.WriteLine($"{source} data does not exist.");
                            Environment.Exit(1);
                        }
                        break;
                    case "wmts":
                    case "xyz":
                    case "tms":
                        try
                        {
                            sources.Add(ParseHttpSource(args, ref idx, batchSize, isBase));
                        }
                        catch
                        {
                            string source = isBase ? "base" : "new";
                            Console.WriteLine($"{source} data does not exist.");
                            Environment.Exit(1);
                        }
                        break;
                    default:
                        throw new Exception($"Currently there is no support for the data type '{args[idx]}'");
                }
                isBase = false;
            }
            return sources;
        }

        private static Data ParseFileSource(string[] args, ref int idx, int batchSize, bool isBase)
        {
            const int paramCount = 3;
            validateSourceLength(args, idx, paramCount);
            string sourceType = args[idx];
            string sourcePath = args[idx + 1];
            bool isOneXOne = bool.Parse(args[idx + 2]);
            idx += paramCount;
            return Data.CreateDatasource(sourceType, sourcePath, batchSize, isOneXOne, isBase);
        }

        private static Data ParseHttpSource(string[] args, ref int idx, int batchSize, bool isBase)
        {
            const int paramCount = 6;
            validateSourceLength(args, idx, paramCount);
            string sourceType = args[idx];
            string sourcePath = args[idx + 1];
            string[] bboxParts = args[idx + 2].Split(',');
            int minZoom = int.Parse(args[idx + 3]);
            int maxZoom = int.Parse(args[idx + 4]);
            bool isOneXOne = bool.Parse(args[idx + 5]);
            Extent extent = new Extent
            {
                minX = double.Parse(bboxParts[0]),
                minY = double.Parse(bboxParts[1]),
                maxX = double.Parse(bboxParts[2]),
                maxY = double.Parse(bboxParts[3])
            };
            idx += paramCount;
            return Data.CreateDatasource(sourceType, sourcePath, batchSize,isBase,extent,maxZoom,minZoom, isOneXOne);
        }

        private static HashSet<string> sourceTypes = new HashSet<string>(new[] { "fs", "s3", "gpkg", "wmts", "tms", "xyz" });
        private static void validateSourceLength(string[] args, int startIdx, int expectedSourceLength)
        {
            for(int i = startIdx +1; i < startIdx + expectedSourceLength; i++)
            {
                if (sourceTypes.Contains(args[i].ToLower()))
                {
                    Console.WriteLine($"invalid source parameters for {args[startIdx]} {args[startIdx + 1]}");
                    PrintHelp(args[0]);
                    Environment.Exit(1);
                }
            }
        }
    }
}
