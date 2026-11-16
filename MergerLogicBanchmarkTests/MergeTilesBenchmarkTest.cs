using BenchmarkDotNet.Attributes;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using Microsoft.Extensions.Logging;
using Moq;
using static MergerLogic.ImageProcessing.TileFormatStrategy;

namespace MergerLogicBanchmarkTests
{
    public class MergeTilesBenchmarkTest
    {
        #region mocks

        private MockRepository _mockRepository;
        private TileMerger _testTileMerger;
        private Coord targetCoordLowZoom;
        private List<CorrespondingTileBuilder> tileBuilderList;
        private TileFormatStrategy tileFormatStrategy;

        Tile testTilePNG1;
        Tile testTilePNG2;

        #endregion

        [GlobalSetup]
        public void GlobalSetup()
        {
            //Write your initialization code here
        }

        [ParamsAllValues]
        public FormatStrategy Strategy { get; set; }

        [ParamsAllValues]
        public TileFormat Format { get; set; }

        [Params(true, false)]
        public bool UploadOnly { get; set; }

        public MergeTilesBenchmarkTest()
        {
            this._mockRepository = new MockRepository(MockBehavior.Loose);

            var metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
            var tileScalerLoggerMock = this._mockRepository.Create<ILogger<TileScaler>>();
            var tileMergerLoggerMock = this._mockRepository.Create<ILogger<TileMerger>>();

            var testTileScaler = new TileScaler(metricsProviderMock.Object, tileScalerLoggerMock.Object);
            this._testTileMerger = new TileMerger(testTileScaler, tileMergerLoggerMock.Object);

            targetCoordLowZoom = new Coord(5, 0, 0);

            testTilePNG1 = new Tile(targetCoordLowZoom, File.ReadAllBytes(Path.Combine(".\\", "TestImages", "2.png")));
            testTilePNG2 = new Tile(targetCoordLowZoom, File.ReadAllBytes(Path.Combine(".\\", "TestImages", "1.png")));


            tileBuilderList = new List<CorrespondingTileBuilder>()
            {
                () => testTilePNG1, () => testTilePNG2
            };
        }

        [Benchmark]
        public void MergeTiles_Benchmark()
        {
            tileFormatStrategy = new TileFormatStrategy(this.Format, this.Strategy);
            //Write your code here   
            var result = this._testTileMerger.MergeTiles(tileBuilderList, targetCoordLowZoom, tileFormatStrategy, this.UploadOnly);
        }
    }
}
