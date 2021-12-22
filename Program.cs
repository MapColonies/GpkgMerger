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
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            // Require input of 2 paths (base and new gpkg) and wanted batch size
            if (args.Length != 6)
            {
                Console.WriteLine($"Usage: {args[0]} <base_type> <path_to_base_datasource> <new_type> <path_to_new_datasource> <batch_size>");
                Console.WriteLine($"Example: {args[0]} gpkg area1.gpkg gpkg area2.gpkg 1000");
                Console.WriteLine($"Example: {args[0]} s3 /path1/on/s3 s3 /path2/on/s3 1000");
                return;
            }

            string baseType = args[1];
            string newType = args[3];
            string basePath = args[2];
            string newPath = args[4];
            int batchSize = int.Parse(args[5]);

            // char* mergeType = getMergeType();
            // if (mergeType == "GPKG")
            // {
            //     File.Exists();
            //     File.Exists();
            //     startGpkgProccess(basePath, newPath, batchSize);
            // }
            // else
            // {
            //     Directory.Exists();
            //     Directory.Exists();
            //     startFolderProccess(basePath, newPath, batchSize);
            // }

            // Data baseData = new Gpkg(fullBasePath, 1000);
            // Data newData = new Gpkg(fullNewPath, 1000);

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
            TimeSpan ts = stopWatch.Elapsed;

            TimeUtils.PrintElapsedTime("Total runtime", ts);
        }
    }
}
