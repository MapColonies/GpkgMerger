using MergerCli.Utils;
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

            // Require input of wanted batch size and 2 types and paths (base and new gpkg)
            if (args.Length < 6 && args.Length != 2)
            {
                string programName = args[0];
                Console.WriteLine($@"Usage:

                                    Supported sources parameters:
                                        2X1 EPSG:4326 web sources (cant be base source):
                                            <'xyz' / 'wmts' / 'tms'> <url template> <bbox - in format 'minX,minY,maxX,maxY'> <min zoom> <max zoom>
                                        file sources:
                                            <'fs' / 's3' / 'gpkg'> <path>   

                                    Single source: {programName} <batch_size> <base source> <addiotional source>
                                    Examples:
                                    {programName} 1000 gpkg area1.gpkg gpkg area2.gpkg
                                    {programName} 1000 s3 /path1/on/s3 s3 /path2/on/s3
                                    {programName} 1000 s3 /path/on/s3 gpkg geo.gpkg
                                    {programName} 1000 s3 /path/on/s3 xyz http://xyzSourceUrl/{{z}}/{{x}}/{{y}}.png -180,-90,180,90 0 21
                                    
                                    Multi source: {programName} <batch_size> <base source> <addiotional source> [<another source source>...]
                                    Examples:
                                    {programName} 1000 gpkg geo.gpkg gpkg area1.gpkg gpkg area2.gpkg gpkg area3.gpkg
                                    {programName} 1000 gpkg geo.gpkg s3 area1 s3 area2 s3 area3
                                    {programName} 1000 s3 geo gpkg area1 s3 area2 gpkg area3 wmts http://wmtsSourceUrl/{{TileMatrix}}/{{TileCol}}/{{TileRow}}.png -180,-90,180,90 0 21

                                    Resume: {programName} <path to status file>.
                                    status file named 'status.json' will be created in running directory on incomplete merges.
                                    **** please note ***
                                    resuming partial merge with FS sources may result in incomplete data after merge as file order may not be consistent.
                                    FS file order should be consistent at least in most use cases (os and fs type combination) but is not guaranteed to be.
                                    Example:
                                    {programName} status.json
                                                             
                                    Minimal requirement is supplying at least one source.");
                return;
            }

            PrepareStatusManger(ref args);

            int batchSize = int.Parse(args[1]);
            int sourceIndex = 2;
            int parameterCount = 0;
            Data baseData;

            // Create base data source and make sure it exists
            try
            {
                baseData = CreateSource(args, batchSize, sourceIndex, true, out parameterCount);
            }
            catch
            {
                Console.WriteLine("Base data does not exist.");
                return;
            }

            try
            {
                sourceIndex += parameterCount;

                while (args.Length > sourceIndex + 1)
                {
                    Data newData;
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();

                    string newType = args[sourceIndex];
                    string newPath = args[sourceIndex + 1];

                    //skip completed layers on resume
                    if (batchStatusManager.IsLayerCompleted(newPath))
                    {
                        sourceIndex += GetParameterCount(newType);
                        continue;
                    }

                    // Create new data source and make sure it exists
                    try
                    {
                        newData = CreateSource(args, batchSize, sourceIndex, false, out parameterCount);
                    }
                    catch
                    {
                        Console.WriteLine("New data does not exist");
                        return;
                    }

                    Proccess.Start(baseData, newData, batchSize, batchStatusManager);

                    // Do some calculation.
                    stopWatch.Stop();

                    // Get the elapsed time as a TimeSpan value.
                    ts = stopWatch.Elapsed;
                    TimeUtils.PrintElapsedTime($"{newPath} merge runtime", ts);

                    bool validate = bool.Parse(Configuration.Instance.GetConfiguration("GENERAL", "validate"));
                    if (validate)
                    {
                        // Reset stopwatch for validation time measure
                        stopWatch.Reset();
                        stopWatch.Start();

                        Console.WriteLine("Validating merged data sources");
                        Proccess.Validate(baseData, newData);

                        stopWatch.Stop();
                        // Get the elapsed time as a TimeSpan value.
                        ts = stopWatch.Elapsed;
                        TimeUtils.PrintElapsedTime($"{newPath} validation time", ts);
                    }

                    // Move to next source
                    sourceIndex += parameterCount;
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

        private static Data CreateSource(string[] args,int batchSize, int startIndex, bool isBase, out int parameterCount)
        {
            string sourceType = args[startIndex];
            string sourcePath = args[startIndex + 1];
            parameterCount = GetParameterCount(sourceType);
            if( parameterCount == 2)
            {
                return Data.CreateDatasource(sourceType, sourcePath, batchSize, isBase);
            } else if ( parameterCount == 5)
            {
                string[] bboxParts = args[startIndex + 2].Split(',');
                int minZoom = int.Parse(args[startIndex + 3]);
                int maxZoom = int.Parse(args[startIndex + 4]);
                Extent extent = new Extent
                {
                    minX = double.Parse(bboxParts[0]),
                    minY = double.Parse(bboxParts[1]),
                    maxX = double.Parse(bboxParts[2]),
                    maxY = double.Parse(bboxParts[3])
                };
                return Data.CreateDatasource(sourceType, sourcePath, batchSize, isBase, extent, maxZoom, minZoom);
            } else
            {
                throw new Exception($"Currently there is no support for the data type '{sourceType}'");
            }
        }

        private static int GetParameterCount(string type)
        {
            string lowerType = type.ToLower();
            switch (lowerType)
            {
                case "fs":
                case "s3":
                case "gpkg":
                    return 2;
                case "wmts":
                case "xyz":
                case "tms":
                    return 5;
                default:
                    throw new Exception($"Currently there is no support for the data type '{type}'");
            }
        }

    }
}
