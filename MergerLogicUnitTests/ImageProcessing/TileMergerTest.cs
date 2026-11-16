using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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
            this._testTileMerger = new TileMerger(testTileScaler, tileMergerLoggerMock.Object);
        }

        #region MergeTiles

        public static IEnumerable<object[]> GetMergeTilesTestParameters()
        {
            var targetCoordLowZoom = new Coord(5, 0, 0);
            var targetCoordMediumZoom = new Coord(14, 0, 0);
            var targetCoordHighZoom = new Coord(15, 0, 0);

            #region Regular merge test cases
            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("2_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("3_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("3.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("3_4_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png), TileFormat.Png, false,
                File.ReadAllBytes("2_1_merged.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, false, File.ReadAllBytes("2_1_merged.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, false, File.ReadAllBytes("2_1_merged.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("3_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("3_4_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("5.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("5.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("5.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordLowZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("3_1_merged_upscaled_5_15.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordMediumZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("3_1_merged_upscaled_14_15.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("4.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("3_2_1_merged.jpeg"),
            };
            #endregion

            #region Test cases for grayscale
            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("grayscale_paletted.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("6_rgb.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("grayscale_paletted_6_rgb_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("grayscale_paletted.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("6_paletted.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("grayscale_paletted_6_paletted_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("grayscale_rgb.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("6_rgb.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("grayscale_rgb_6_rgb_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("grayscale_rgb.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("6_paletted.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("grayscale_rgb_6_paletted_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("grayscale.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("6_paletted.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("grayscale_6_paletted_merged.jpeg"),
            };
            #endregion

            #region Test cases for ignoring target
            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("1.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("1.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("3.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("4.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png), TileFormat.Png, true,
                File.ReadAllBytes("1.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, true, File.ReadAllBytes("1.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, true, File.ReadAllBytes("1.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, true, File.ReadAllBytes("1.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("2_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("3_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("3.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("4.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png), TileFormat.Png, true,
                File.ReadAllBytes("2_1_merged.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, true, File.ReadAllBytes("2_1_merged.png"),
            };
            #endregion
        }

        [TestMethod]
        [TestCategory("MergeTiles")]
        [DynamicData(nameof(GetMergeTilesTestParameters), DynamicDataSourceType.Method)]
        public void MergeTiles(Tile[] tiles, Coord targetCoord, TileFormatStrategy strategy, TileFormat expectedForamt, bool uploadOnly, byte[] expectedTileBytes)
        {
            var tileBuilders = tiles.Select<Tile, CorrespondingTileBuilder>(tile => () => tile).ToList();
            var result = this._testTileMerger.MergeTiles(tileBuilders, targetCoord, strategy, uploadOnly);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedForamt, result.Format);
            CollectionAssert.AreEqual(expectedTileBytes, result.GetImageBytes());
        }

        [TestMethod]
        [TestCategory("MergeTiles")]
        public void MergeTilesStatsBlendedTargetAndSource()
        {
            var targetCoord = new Coord(15, 0, 0);
            // target (index 0) is transparent, source (last) is transparent -> both enter the stack
            var tiles = new[]
            {
                new Tile(targetCoord, File.ReadAllBytes("2.png")),
                new Tile(targetCoord, File.ReadAllBytes("1.png"))
            };
            var tileBuilders = tiles.Select<Tile, CorrespondingTileBuilder>(tile => () => tile).ToList();

            var result = this._testTileMerger.MergeTiles(tileBuilders, targetCoord, new TileFormatStrategy(TileFormat.Png), out var stats);

            Assert.IsNotNull(result);
            Assert.IsTrue(stats.TargetUsed);
            Assert.IsTrue(stats.AnySourceUsed);
        }

        [TestMethod]
        [TestCategory("MergeTiles")]
        public void MergeTilesStatsOpaqueSourceOverTarget()
        {
            var targetCoord = new Coord(15, 0, 0);
            // opaque source (last) short-circuits before the target (index 0) is reached
            var tiles = new[]
            {
                new Tile(targetCoord, File.ReadAllBytes("1.png")),
                new Tile(targetCoord, File.ReadAllBytes("3.jpeg"))
            };
            var tileBuilders = tiles.Select<Tile, CorrespondingTileBuilder>(tile => () => tile).ToList();

            var result = this._testTileMerger.MergeTiles(tileBuilders, targetCoord, new TileFormatStrategy(TileFormat.Jpeg), out var stats);

            Assert.IsNotNull(result);
            Assert.IsFalse(stats.TargetUsed);
            Assert.IsTrue(stats.AnySourceUsed);
        }

        [TestMethod]
        [TestCategory("MergeTiles")]
        public void MergeTilesStatsUploadOnly()
        {
            var targetCoord = new Coord(15, 0, 0);
            var tiles = new[]
            {
                new Tile(targetCoord, File.ReadAllBytes("2.png")),
                new Tile(targetCoord, File.ReadAllBytes("1.png"))
            };
            var tileBuilders = tiles.Select<Tile, CorrespondingTileBuilder>(tile => () => tile).ToList();

            var result = this._testTileMerger.MergeTiles(tileBuilders, targetCoord, new TileFormatStrategy(TileFormat.Jpeg), out var stats, uploadOnly: true);

            Assert.IsNotNull(result);
            Assert.IsFalse(stats.TargetUsed);
            Assert.IsTrue(stats.AnySourceUsed);
        }

        #endregion
    }
}
