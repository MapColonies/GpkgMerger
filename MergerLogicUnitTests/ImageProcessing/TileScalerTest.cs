using Castle.Components.DictionaryAdapter.Xml;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using MergerLogic.Utils;
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
        private Mock<IConfigurationManager> _configurationManagerMock;

        private TileScaler _testTileScaler;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._mockRepository = new MockRepository(MockBehavior.Loose);

            var metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
            var tileScalerLoggerMock = this._mockRepository.Create<ILogger<TileScaler>>();
            this._configurationManagerMock = this._mockRepository.Create<IConfigurationManager>();

            this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<int>("GENERAL", "allowedPixelSize"))
                .Returns(256);

            this._testTileScaler = new TileScaler(metricsProviderMock.Object, tileScalerLoggerMock.Object, this._configurationManagerMock.Object);
        }

        #region Upscale


        public static IEnumerable<object?[]> GetUpscaleTilesTestParameters()
        {
            yield return new object[] {
                File.ReadAllBytes("1.png"),
                new Coord(16, 0, 0),
                new Coord(17, 0, 0),
                File.ReadAllBytes("1_upscaled_16_17.png"),
            };

            yield return new object[] {
                File.ReadAllBytes("4.jpeg"),
                new Coord(3, 0, 0),
                new Coord(13, 0, 0),
                File.ReadAllBytes("4_upscaled_3_13.jpeg"),
            };

            yield return new object[] {
                File.ReadAllBytes("5.png"),
                new Coord(16, 0, 0),
                new Coord(18, 0, 0),
                File.ReadAllBytes("5_upscaled_16_18.png"),
            };

            yield return new object[] {
                File.ReadAllBytes("5_8bit.png"),
                new Coord(16, 0, 0),
                new Coord(18, 0, 0),
                File.ReadAllBytes("5_8bit_upscaled_16_18.png"),
            };

            yield return new object[] {
                File.ReadAllBytes("5_24bit.png"),
                new Coord(16, 0, 0),
                new Coord(18, 0, 0),
                File.ReadAllBytes("5_24bit_upscaled_16_18.png"),
            };

            yield return new object[] {
                File.ReadAllBytes("5_32bit.png"),
                new Coord(16, 0, 0),
                new Coord(18, 0, 0),
                File.ReadAllBytes("5_upscaled_16_18.png"),
            };

            yield return new object[] {
                File.ReadAllBytes("5_64bit.png"),
                new Coord(16, 0, 0),
                new Coord(18, 0, 0),
                File.ReadAllBytes("5_upscaled_16_18.png"),
            };

            yield return new object[] {
                File.ReadAllBytes("3.jpeg"),
                new Coord(9, 0, 0),
                new Coord(10, 0, 0),
                File.ReadAllBytes("3_upscaled_9_10.jpeg"),
            };


            yield return new object?[] {
                File.ReadAllBytes("empty_tile.png"),
                new Coord(3, 0, 0),
                new Coord(4, 0, 0),
                null,
            };
        }
        [TestMethod]
        [TestCategory("Upscale")]
        [DynamicData(nameof(GetUpscaleTilesTestParameters), DynamicDataSourceType.Method)]
        public void Upscale(byte[] tileBytes, Coord baseTileCoord, Coord targetCoord, byte[]? expectedTileBytes)
        {
            var testTile = new Tile(this._configurationManagerMock.Object, baseTileCoord, tileBytes);
            var resultTile = this._testTileScaler.Upscale(testTile, targetCoord);

            if (expectedTileBytes is null)
            {
                Assert.IsNull(resultTile);
            }
            else
            {
                Assert.IsNotNull(resultTile);
                CollectionAssert.AreEqual(expectedTileBytes, resultTile.GetImageBytes());
            }
        }

        #endregion
    }
}
