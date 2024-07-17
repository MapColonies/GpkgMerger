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

        private Mock<IConfigurationManager> _configurationManagerMock;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._mockRepository = new MockRepository(MockBehavior.Loose);

            var metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
            var tileScalerLoggerMock = this._mockRepository.Create<ILogger<TileScaler>>();
            var tileMergerLoggerMock = this._mockRepository.Create<ILogger<TileMerger>>();
            this._configurationManagerMock = this._mockRepository.Create<IConfigurationManager>();

            this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<long>("GENERAL", "allowedPixelSize"))
                .Returns(256);

            var testTileScaler = new TileScaler(metricsProviderMock.Object, tileScalerLoggerMock.Object, this._configurationManagerMock.Object);
            this._testTileMerger = new TileMerger(testTileScaler, tileMergerLoggerMock.Object, this._configurationManagerMock.Object);
        }

        #region MergeTiles

        public IEnumerable<object[]> GetMergeTilesTestParameters()
        {
            var targetCoordLowZoom = new Coord(5, 0, 0);
            var targetCoordMediumZoom = new Coord(14, 0, 0);
            var targetCoordHighZoom = new Coord(15, 0, 0);

            #region Regular merge test cases
            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("2_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("3_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("3.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("3_4_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png), TileFormat.Png, false,
                File.ReadAllBytes("2_1_merged.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, false, File.ReadAllBytes("2_1_merged.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, false, File.ReadAllBytes("2_1_merged.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("3_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("3_4_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("5.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("5.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("5.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordLowZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("3_1_merged_upscaled_5_15.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordMediumZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("3_1_merged_upscaled_14_15.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, false,
                File.ReadAllBytes("4.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Jpeg, false, File.ReadAllBytes("3_2_1_merged.jpeg"),
            };
            #endregion

            #region Test cases for ignoring target
            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("1.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("1.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("3.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("4.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png), TileFormat.Png, true,
                File.ReadAllBytes("1.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, true, File.ReadAllBytes("1.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, true, File.ReadAllBytes("1.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png, TileFormatStrategy.FormatStrategy.Mixed),
                TileFormat.Png, true, File.ReadAllBytes("1.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("2_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("3_1_merged.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("3.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("4.jpeg"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Jpeg), TileFormat.Jpeg, true,
                File.ReadAllBytes("4.jpeg"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
                }, targetCoordHighZoom, new TileFormatStrategy(TileFormat.Png), TileFormat.Png, true,
                File.ReadAllBytes("2_1_merged.png"),
            };

            yield return new object[] {
                new Tile[] {
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("3.jpeg")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("2.png")),
                    new Tile(this._configurationManagerMock.Object, targetCoordHighZoom, File.ReadAllBytes("1.png"))
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

        #endregion
    }
}
