using System;
using System.Diagnostics;
using GpkgMerger.Src.DataTypes;
using GpkgMerger.Src.Utils;

namespace GpkgMerger
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch totalTimeStopWatch = new Stopwatch();
            totalTimeStopWatch.Start();
            TimeSpan ts;

            // Require input of wanted batch size and 2 types and paths (base and new gpkg)
            if (args.Length < 6)
            {
                Console.WriteLine($"Usage: {args[0]} <batch_size> <base_type> <path_to_base_datasource> <new_type> <path_to_new_datasource>");
                Console.WriteLine($"Example: {args[0]} 1000 gpkg area1.gpkg gpkg area2.gpkg");
                Console.WriteLine($"Example: {args[0]} 1000 s3 /path1/on/s3 s3 /path2/on/s3");
                Console.WriteLine($"Example: {args[0]} 1000 s3 /path/on/s3 gpkg geo.gpkg");
                return;
            }

            int batchSize = int.Parse(args[1]);
            string baseType = args[2];
            string basePath = args[3];

            int sourceIndex = 4;

            while (args.Length > sourceIndex + 1)
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string newType = args[sourceIndex];
                string newPath = args[sourceIndex + 1];

                // Console.WriteLine($"type: {newType}, path: {newPath}");
                // sourceIndex += 2;
                // continue;

                Data baseData = Data.CreateDatasource(baseType, basePath, batchSize);
                Data newData = Data.CreateDatasource(newType, newPath, batchSize);

                if (!baseData.Exists())
                {
                    Console.WriteLine("Base data does not exist.");
                    return;
                }

                if (!newData.Exists())
                {
                    Console.WriteLine("New data does not exist");
                    return;
                }

                Src.Proccess.Start(baseData, newData);

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

                    Console.WriteLine("Validating merged datasources");
                    Src.Proccess.Validate(baseData, newData);

                    stopWatch.Stop();
                    // Get the elapsed time as a TimeSpan value.
                    ts = stopWatch.Elapsed;
                    TimeUtils.PrintElapsedTime($"{newPath} validation time", ts);
                }

                // Move to next source
                sourceIndex += 2;
            }

            totalTimeStopWatch.Stop();
            // Get the elapsed time as a TimeSpan value.
            ts = totalTimeStopWatch.Elapsed;
            TimeUtils.PrintElapsedTime("Total runtime", ts);
        }
    }
}
