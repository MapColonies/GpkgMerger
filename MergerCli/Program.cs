using MergerCli.Utils;
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
            if ((args.Length < 6 && args.Length != 2) || args.Length % 2 != 0)
            {
                string programName = args[0];
                Console.WriteLine($@"Usage:
                                    Single source: {programName} <batch_size> <base_type> <path_to_base_datasource> <new_type> <path_to_new_datasource>
                                    Examples:
                                    {programName} 1000 gpkg area1.gpkg gpkg area2.gpkg
                                    {programName} 1000 s3 /path1/on/s3 s3 /path2/on/s3
                                    {programName} 1000 s3 /path/on/s3 gpkg geo.gpkg
                                    
                                    Multi source: {programName} <batch_size> <base_type> <path_to_base_datasource> [<new_type> <path_to_new_datasource>...]
                                    Examples:
                                    {programName} 1000 gpkg geo.gpkg gpkg area1.gpkg gpkg area2.gpkg gpkg area3.gpkg
                                    {programName} 1000 gpkg geo.gpkg s3 area1 s3 area2 s3 area3
                                    {programName} 1000 s3 geo gpkg area1 s3 area2 gpkg area3

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
            string baseType = args[2];
            string basePath = args[3];

            Data baseData;

            // Create base data source and make sure it exists
            try
            {
                baseData = Data.CreateDatasource(baseType, basePath, batchSize, true);
            }
            catch
            {
                Console.WriteLine("Base data does not exist.");
                return;
            }

            try
            {
                int sourceIndex = 4;

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
                        sourceIndex += 2;
                        continue;
                    }

                    // Create new data source and make sure it exists
                    try
                    {
                        newData = Data.CreateDatasource(newType, newPath, batchSize);
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
                    sourceIndex += 2;
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

    }
}
