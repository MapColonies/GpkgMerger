using MergerCli.Utils;
using MergerLogic.DataTypes;
using MergerLogic.Extentions;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;
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
            if (args.Length < 6 && args.Length != 2)
            {
                Console.WriteLine("invalid command.");
                PrintHelp(programName);
                return;
            }
            ServiceProvider container = CreateContianer();

            PrepareStatusManger(ref args);

            int batchSize = int.Parse(args[1]);
            List<IData> sources;
            try
            {
                var parser = container.GetRequiredService<ISourceParser>();
                sources = parser.ParseSources(args, batchSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                PrintHelp(args[0]);
                return;
            }

            IData baseData = sources[0];
            if (sources.Count < 2)
            {
                Console.WriteLine("minimum of 2 sources is required");
                PrintHelp(programName);
                return;
            }

            var proccess = container.GetRequiredService<IProcess>();
            var timeUtils = container.GetRequiredService<ITimeUtils>();
            try
            {
                var config = container.GetService<IConfigurationManager>();
                bool validate = bool.Parse(config.GetConfiguration("GENERAL", "validate"));
                for (int i = 1; i < sources.Count; i++)
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    if (batchStatusManager.IsLayerCompleted(sources[i].Path))
                    {
                        continue;
                    }
                    proccess.Start(baseData, sources[i], batchSize, batchStatusManager);
                    stopWatch.Stop();

                    // Get the elapsed time as a TimeSpan value.
                    ts = stopWatch.Elapsed;
                    timeUtils.PrintElapsedTime($"{sources[i].Path} merge runtime", ts);


                    if (validate)
                    {
                        // Reset stopwatch for validation time measure
                        stopWatch.Reset();
                        stopWatch.Start();

                        Console.WriteLine("Validating merged data sources");
                        proccess.Validate(baseData, sources[i]);

                        stopWatch.Stop();
                        // Get the elapsed time as a TimeSpan value.
                        ts = stopWatch.Elapsed;
                        timeUtils.PrintElapsedTime($"{sources[i].Path} validation time", ts);
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
            timeUtils.PrintElapsedTime("Total runtime", ts);
            done = true;
        }

        private static ServiceProvider CreateContianer()
        {
            return new ServiceCollection()
                .RegisterMergerLogicType()
                .AddSingleton<IProcess, Process>()
                .AddSingleton<ISourceParser, SourceParser>()
                .BuildServiceProvider();
        }

        private static void PrintHelp(string programName)
        {
            Console.WriteLine($@"Usage:

                                    Supported sources parameters:
                                        web sources (cant be base source):
                                            <'xyz' / 'wmts' / 'tms'> <url template> <bbox - in format 'minX,minY,maxX,maxY'> <min zoom> <max zoom> [--1x1] [--UL / --LL] 
                                        file sources:
                                            <'fs' / 's3' / 'gpkg'> <path> [--1x1] [--UL / --LL] 
                                        **** please note all layers must be 2X1 EPSG:4326 layers ****
                                    
                                    merge sources: {programName} <batch_size> <base source> <addiotional source> [<another source>...]
                                    Examples:
                                    {programName} 1000 gpkg area1.gpkg gpkg area2.gpkg
                                    {programName} 1000 s3 /path1/on/s3 s3 /path2/on/s3
                                    {programName} 1000 s3 /path/on/s3 gpkg geo.gpkg --UL
                                    {programName} 1000 s3 /path/on/s3 xyz http://xyzSourceUrl/{{z}}/{{x}}/{{y}}.png -180,-90,180,90 0 21 --1x1 --UL
                                    {programName} 1000 gpkg geo.gpkg gpkg 1x1.gpkg --1x1 gpkg area2.gpkg gpkg area3.gpkg
                                    {programName} 1000 gpkg geo.gpkg s3 area1 s3 area2 s3 area3
                                    {programName} 1000 s3 geo gpkg area1 s3 1x1 --1x1 gpkg area3 wmts http://wmtsSourceUrl/{{TileMatrix}}/{{TileCol}}/{{TileRow}}.png -180,-90,180,90 0 21

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
    }
}
