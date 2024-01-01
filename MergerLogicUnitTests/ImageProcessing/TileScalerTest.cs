using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.IO;

namespace MergerLogicUnitTests.ImageProcessing
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("imageProcessing")]
    [DeploymentItem(@"../../../ImageProcessing/TestImages")]
    public class TileScalerTest
    {

        #region mocks

        private MockRepository _mockRepository;

        private TileScaler _testTileScaler;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._mockRepository = new MockRepository(MockBehavior.Loose);

            var metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
            var tileScalerLoggerMock = this._mockRepository.Create<ILogger<TileScaler>>();

            this._testTileScaler = new TileScaler(metricsProviderMock.Object, tileScalerLoggerMock.Object);
        }

        #region Upscale


        public static IEnumerable<object[]> GetUpscaleTilesTestParameters()
        {
            yield return new object[] {
                File.ReadAllBytes("3.jpeg"),
                new Coord(9, 0, 0),
                new Coord(10, 0, 0),
                File.ReadAllBytes("3_upscaled_9_10.jpeg"),
            };
        }
        [TestMethod]
        [TestCategory("Upscale")]
        [DynamicData(nameof(GetUpscaleTilesTestParameters), DynamicDataSourceType.Method)]
        public void Upscale(byte[] tileBytes, Coord baseTileCoord, Coord targetCoord, byte[] expectedTileBytes)
        {
            var testTile = new Tile(baseTileCoord, tileBytes);
            var resultTile = this._testTileScaler.Upscale(testTile, targetCoord);

            Assert.IsNotNull(resultTile);
            CollectionAssert.AreEqual(expectedTileBytes, resultTile.GetImageBytes());
        }

        #endregion
    }
}
