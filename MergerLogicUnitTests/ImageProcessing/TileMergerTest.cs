using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MergerLogicUnitTests.ImageProcessing
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("imageProcessing")]
    [DeploymentItem(@"../../../ImageProcessing/TestImages")]
    public class TileMergerTest
    {

        #region mocks

        private MockRepository _mockRepository;

        private TileMerger _testTileMerger;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._mockRepository = new MockRepository(MockBehavior.Loose);

            var metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
            var tileScalerLoggerMock = this._mockRepository.Create<ILogger<TileScaler>>();
            var tileMergerLoggerMock = this._mockRepository.Create<ILogger<TileMerger>>();

            var testTileScaler = new TileScaler(metricsProviderMock.Object, tileScalerLoggerMock.Object);
            var testImageFormatter = new ImageFormatter();

            this._testTileMerger = new TileMerger(testTileScaler, testImageFormatter, tileMergerLoggerMock.Object);
        }

        #region MergeTiles

        public static IEnumerable<object[]> GetMergeTilesTestParameters()
        {
            var targetCoord = new Coord(9, 608, 343);

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoord, File.ReadAllBytes("2.png")),
                    new Tile(targetCoord, File.ReadAllBytes("1.png"))
                }, targetCoord, TileFormat.Png,
                File.ReadAllBytes("2_1_merged.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoord, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoord, File.ReadAllBytes("1.png"))
                }, targetCoord, TileFormat.Jpeg,
                File.ReadAllBytes("3_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoord, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoord, File.ReadAllBytes("4.jpeg"))
                }, targetCoord, TileFormat.Jpeg,
                File.ReadAllBytes("3_4_merged.jpeg"),
            };
        }

        [TestMethod]
        [TestCategory("MergeTiles")]
        [DynamicData(nameof(GetMergeTilesTestParameters), DynamicDataSourceType.Method)]
        public void MergeTiles(Tile[] tiles, Coord targetCoord, TileFormat tileFormat, byte[] expectedTileBytes)
        {
            var tileBuilders = tiles.Select<Tile, CorrespondingTileBuilder>(tile => () => tile).ToList();
            var result = this._testTileMerger.MergeTiles(tileBuilders, targetCoord, tileFormat);


            Console.WriteLine($"-------- VITTTT -------------");
            Console.WriteLine($"{Convert.ToBase64String(result)}");
            Console.WriteLine($"-------- VITTTT -------------");

            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(expectedTileBytes, result);
        }

        #endregion
    }
}
