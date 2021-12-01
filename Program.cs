using System;
using System.Diagnostics;
using System.IO;
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
            if (args.Length != 4)
            {
                Console.WriteLine($"Usage: {args[0]} <path_to_base_gpkg> <path_to_new_gpkg> <batch_size>");
                Console.WriteLine($"Example: {args[0]} area1.gpkg area2.gpkg 1000");
                return;
            }

            // TODO: check if path exists and is file
            string path1 = args[1];
            string path2 = args[2];

            // Get full path to gpkg files
            string fullPath1 = Path.GetFullPath(path1);
            string fullPath2 = Path.GetFullPath(path2);

            int batchSize = int.Parse(args[3]);

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

            if (!File.Exists(fullPath1))
            {
                Console.WriteLine($"No such file '{fullPath1}'");
                return;
            }
            if (!File.Exists(fullPath2))
            {
                Console.WriteLine($"No such file '{fullPath2}'");
                return;
            }

            Data baseData = new Gpkg(DataType.GPKG, path1, 1000);
            Data newData = new Gpkg(DataType.GPKG, path2, 1000);

            Src.Proccess.Start(baseData, newData);

            // Do some calculation.
            stopWatch.Stop();

            // Get the elapsed time as a TimeSpan value.
            TimeSpan ts = stopWatch.Elapsed;

            TimeUtils.PrintElapsedTime("Total runtime", ts);
        }
    }
}
