using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using MergerLogicBanchmarkTests;
using Microsoft.Extensions.Logging;
using Moq;
using static MergerLogic.ImageProcessing.TileFormatStrategy;

namespace MergerLogicBenchmarksTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<UpscaleBenchmarkTest>();
            // var summary2 = BenchmarkRunner.Run<MergeTilesBenchmarkTest>();
        }
    }
}

