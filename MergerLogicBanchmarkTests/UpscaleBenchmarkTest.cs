using BenchmarkDotNet.Attributes;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using Microsoft.Extensions.Logging;
using Moq;

namespace MergerLogicBanchmarkTests
{
    public class UpscaleBenchmarkTest
    {
        #region mocks

        private MockRepository _mockRepository;

        private TileScaler _testTileScaler;

        private Tile testTileJpeg;
        private Tile testTilePNG;

        #endregion

        [GlobalSetup]
        public void GlobalSetup()
        {
            //Write your initialization code here
        }

        public UpscaleBenchmarkTest()
        {
            testTileJpeg = new Tile(new Coord(3, 0, 0), System.IO.File.ReadAllBytes(Path.Combine(".\\", "TestImages", "5.jpeg")));
            testTilePNG = new Tile(new Coord(3, 0, 0), System.IO.File.ReadAllBytes(Path.Combine(".\\", "TestImages", "5_64bit.png")));

            this._mockRepository = new MockRepository(MockBehavior.Loose);

            var metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
            var tileScalerLoggerMock = this._mockRepository.Create<ILogger<TileScaler>>();

            this._testTileScaler = new TileScaler(metricsProviderMock.Object, tileScalerLoggerMock.Object);
        }

        [Benchmark]
        public void Upscale_jpeg()
        {
            //Write your code here   
            var resultTile = this._testTileScaler.Upscale(testTileJpeg, new Coord(4, 0, 0));
        }
        [Benchmark]
        public void Upscale_png()
        {
            //Write your code here   
            var resultTile = this._testTileScaler.Upscale(testTilePNG, new Coord(4, 0, 0));
        }
    }
}
