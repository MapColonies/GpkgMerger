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
                                    
                                    Minimal requirement is supplying at least one source.");
                return;
            }

            int batchSize = int.Parse(args[1]);
            string baseType = args[2];
            string basePath = args[3];

            // Create base datasource and make sure it exists
            Data baseData = Data.CreateDatasource(baseType, basePath, batchSize);
            if (!baseData.Exists())
            {
                Console.WriteLine("Base data does not exist.");
                return;
            }

            int sourceIndex = 4;

            while (args.Length > sourceIndex + 1)
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                string newType = args[sourceIndex];
                string newPath = args[sourceIndex + 1];

                // Create new datasource and make sure it exists
                Data newData = Data.CreateDatasource(newType, newPath, batchSize);
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
