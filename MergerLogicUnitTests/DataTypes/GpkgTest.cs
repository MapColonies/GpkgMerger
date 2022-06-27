using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using MergerLogicUnitTests.utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MergerLogicUnitTests.DataTypes
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("gpkg")]
    [TestCategory("gpkgDataSource")]
    public class GpkgTest
    {
        #region mocks

        private MockRepository _repository;
        private Mock<IConfigurationManager> _configurationManagerMock;
        private Mock<IServiceProvider> _serviceProviderMock;
        private Mock<IOneXOneConvertor> _oneXOneConvertorMock;
        private Mock<IUtilsFactory> _utilsFactoryMock;
        private Mock<IGpkgUtils> _gpkgUtilsMock;
        private Mock<ILogger<Gpkg>> _loggerMock;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._configurationManagerMock = this._repository.Create<IConfigurationManager>();
            this._oneXOneConvertorMock = this._repository.Create<IOneXOneConvertor>();
            this._gpkgUtilsMock = this._repository.Create<IGpkgUtils>();
            this._utilsFactoryMock = this._repository.Create<IUtilsFactory>();
            this._utilsFactoryMock.Setup(factory => factory.GetDataUtils<IGpkgUtils>(It.IsAny<string>()))
                .Returns(this._gpkgUtilsMock.Object);
            this._loggerMock = this._repository.Create<ILogger<Gpkg>>(MockBehavior.Loose);
            this._serviceProviderMock = this._repository.Create<IServiceProvider>();
            this._serviceProviderMock.Setup(container =>
                container.GetService(typeof(IOneXOneConvertor))).Returns(this._oneXOneConvertorMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IUtilsFactory)))
                .Returns(this._utilsFactoryMock.Object);
            this._serviceProviderMock.Setup(container =>
                container.GetService(typeof(ILogger<Gpkg>))).Returns(this._loggerMock.Object);
        }

        #region TileExists

        [TestMethod]
        [TestCategory("TileExists")]
        //existing tile
        [DataRow(10, false, 2, 2, 3, true, true)]
        [DataRow(100, true, 2, 2, 3, true, true)]
        [DataRow(10, false, 2, 2, 3, true, false)]
        [DataRow(100, true, 2, 2, 3, true, false)]
        //missing tile
        [DataRow(10, false, 1, 2, 3, false, true)]
        [DataRow(100, true, 1, 2, 3, false, true)]
        [DataRow(10, false, 1, 2, 3, false, false)]
        [DataRow(100, true, 1, 2, 3, false, false)]
        public void TileExistsWithoutConversion(int batchSize, bool isBase, int z, int x, int y, bool expected,
            bool useCords)
        {
            this.SetupRequiredBaseMocks(isBase);
            this._gpkgUtilsMock.Setup(utils => utils.TileExists(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z == 2);
            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, false,
                extent, GridOrigin.UPPER_LEFT);

            if (useCords)
            {
                var cords = new Coord(z, x, y);
                Assert.AreEqual(expected, gpkg.TileExists(cords));
            }
            else
            {
                var tile = new Tile(z, x, y, new byte[] { });
                Assert.AreEqual(expected, gpkg.TileExists(tile));
            }

            this._gpkgUtilsMock.Verify(util => util.TileExists(z, x, y), Times.Once);
            this.VerifyAll();
        }

        [TestMethod]
        [TestCategory("TileExists")]
        //existing tile
        [DataRow(10, false, 2, 2, 2, true, true)]
        [DataRow(100, true, 2, 2, 2, true, true)]
        [DataRow(10, false, 2, 2, 2, true, false)]
        [DataRow(100, true, 2, 2, 2, true, false)]
        //missing tile
        [DataRow(10, false, 1, 2, 3, false, true)]
        [DataRow(100, true, 1, 2, 3, false, true)]
        [DataRow(10, false, 1, 2, 3, false, false)]
        [DataRow(100, true, 1, 2, 3, false, false)]
        //invalid conversion tile
        [DataRow(10, false, 0, 0, 0, false, true)]
        [DataRow(100, true, 0, 0, 0, false, true)]
        [DataRow(10, false, 0, 0, 0, false, false)]
        [DataRow(100, true, 0, 0, 0, false, false)]
        public void TileExistsWithConversion(int batchSize, bool isBase, int z, int x, int y, bool expected,
            bool useCords)
        {
            this.SetupRequiredBaseMocks(isBase);
            if (z != 0)
            {
                this._gpkgUtilsMock.Setup(utils => utils.TileExists(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => z == 2);
            }

            this._oneXOneConvertorMock.Setup(converter => converter.TryFromTwoXOne(It.IsAny<Coord>()))
                .Returns<Coord>(cords => cords.z != 0 ? cords : null);
            //TODO: mock origin convertor?

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, true,
                extent, GridOrigin.LOWER_LEFT);

            if (useCords)
            {
                var cords = new Coord(z, x, y);
                Assert.AreEqual(expected, gpkg.TileExists(cords));
            }
            else
            {
                var tile = new Tile(z, x, y, new byte[] { });
                Assert.AreEqual(expected, gpkg.TileExists(tile));
            }

            this._gpkgUtilsMock.Verify(util => util.TileExists(z, x, It.IsAny<int>()),
                z != 0
                    ? Times.Once
                    : Times.Never); //TODO: replace with specific validation after mocking origin conversion
            //TODO: add conversion specific validation after mocking origin conversion
            this.VerifyAll();
        }

        #endregion

        #region GetCorrespondingTile

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        //existingTile
        [DataRow(10, false, 2, 2, 3, false)]
        [DataRow(100, true, 2, 2, 3, false)]
        //missing tile
        [DataRow(10, false, 1, 2, 3, true)]
        [DataRow(100, true, 1, 2, 3, true)]
        public void GetCorrespondingTileWithoutUpscaleWithoutConversion(int batchSize, bool isBase, int z, int x, int y,
            bool expectedNull)
        {
            this.SetupRequiredBaseMocks(isBase);
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            this._gpkgUtilsMock.Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, false,
                extent, GridOrigin.UPPER_LEFT);

            var cords = new Coord(z, x, y);
            Assert.AreEqual(expectedNull ? null : existingTile, gpkg.GetCorrespondingTile(cords, false));
            this._gpkgUtilsMock.Verify(util => util.GetTile(z, x, y), Times.Once);
            this.VerifyAll();
        }

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        //existing tile
        [DataRow(10, false, 2, 2, 3, false, false)]
        [DataRow(100, true, 2, 2, 3, false, false)]
        [DataRow(10, false, 2, 2, 3, false, true)]
        [DataRow(100, true, 2, 2, 3, false, true)]
        //missing tile
        [DataRow(10, false, 1, 2, 3, true, false)]
        [DataRow(100, true, 1, 2, 3, true, false)]
        [DataRow(10, false, 1, 2, 3, true, true)]
        [DataRow(100, true, 1, 2, 3, true, true)]
        //invalid conversion tile
        [DataRow(10, false, 0, 2, 3, true, false)]
        [DataRow(100, true, 0, 2, 3, true, false)]
        [DataRow(10, false, 0, 2, 3, true, true)]
        [DataRow(100, true, 0, 2, 3, true, true)]
        public void GetCorrespondingTileWithoutUpscaleWithConversion(int batchSize, bool isBase, int z, int x, int y,
            bool expectedNull, bool enableUpscale)
        {
            this.SetupRequiredBaseMocks(isBase);
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            var sequence = new MockSequence();
            this._oneXOneConvertorMock
                .InSequence(sequence)
                .Setup(converter => converter.TryFromTwoXOne(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z != 0 ? new Coord(z, x, y) : null);
            if (z != 0)
            {
                this._gpkgUtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.GetTile(It.IsAny<Coord>()))
                    .Returns<Coord>(cords => cords.z == 2 ? existingTile : nullTile);
                if (z != 1)
                {
                    this._oneXOneConvertorMock
                        .InSequence(sequence)
                        .Setup(converter => converter.ToTwoXOne(It.IsAny<Tile>()))
                        .Returns<Tile>(tile => tile.Z != 0 ? tile : null);
                }
            }

            if (enableUpscale)
            {
                this._oneXOneConvertorMock
                    .InSequence(sequence)
                    .Setup(converter => converter.TryFromTwoXOne(It.IsAny<Coord>()))
                    .Returns<Coord>(cords => cords);
                this._gpkgUtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.GetLastTile(It.IsAny<int[]>(), It.IsAny<Coord>()))
                    .Returns(nullTile);
            }
            //TODO: mock origin convertor?

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, true,
                extent, GridOrigin.LOWER_LEFT);

            var cords = new Coord(z, x, y);
            var res = gpkg.GetCorrespondingTile(cords, enableUpscale);
            if (expectedNull)
            {
                Assert.IsNull(res);
            }
            else
            {
                Assert.IsInstanceOfType(res,
                    typeof(Tile)); //TODO: replace with specific validation after mocking origin conversion
            }

            this._gpkgUtilsMock.Verify(util => util.GetTile(It.IsAny<Coord>()),
                z != 0
                    ? Times.Once
                    : Times.Never); //TODO: replace with specific validation after mocking origin conversion
            //TODO: add conversion specific validation after mocking origin conversion
            this.VerifyAll();
        }

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        [DataRow(10, false)]
        [DataRow(100, true)]
        public void GetCorrespondingTileWithUpscaleWithoutConversion(int batchSize, bool isBase)
        {
            this.SetupRequiredBaseMocks(isBase);
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            var seq = new MockSequence();
            this._gpkgUtilsMock.InSequence(seq)
                .Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(nullTile);
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.GetLastTile(It.IsAny<int[]>(), It.IsAny<Coord>()))
                .Returns<int[], Coord>((cords, baseCords) => baseCords.z == 5 ? existingTile : null);

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, false,
                extent, GridOrigin.UPPER_LEFT);

            var upscaleCoords = new Coord(5, 2, 3);

            Assert.AreEqual(existingTile, gpkg.GetCorrespondingTile(upscaleCoords, true));
            this._gpkgUtilsMock.Verify(utils =>
                utils.GetLastTile(new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 }, upscaleCoords));
            this.VerifyAll();
        }

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        [DataRow(10, false)]
        [DataRow(100, true)]
        public void GetCorrespondingTileWithUpscaleWithConversion(int batchSize, bool isBase)
        {
            this.SetupRequiredBaseMocks(isBase);
            Tile nullTile = null;
            var tile = new Tile(2, 2, 3, new byte[] { });
            var sequence = new MockSequence();

            this._oneXOneConvertorMock.InSequence(sequence).Setup(converter =>
                    converter.TryFromTwoXOne(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => new Coord(z, x, y));
            this._gpkgUtilsMock.InSequence(sequence).Setup(utils => utils.GetTile(It.IsAny<Coord>()))
                .Returns(nullTile);
            this._oneXOneConvertorMock.InSequence(sequence)
                .Setup(converter => converter.TryFromTwoXOne(It.IsAny<Coord>()))
                .Returns<Coord>(cords => cords);
            this._gpkgUtilsMock.InSequence(sequence)
                .Setup(utils => utils.GetLastTile(It.IsAny<int[]>(), It.IsAny<Coord>()))
                .Returns(tile);
            this._oneXOneConvertorMock.InSequence(sequence).Setup(converter => converter.ToTwoXOne(It.IsAny<Tile>()))
                .Returns<Tile>(tile => tile.Z != 0 ? tile : null);
            //TODO: mock origin convertor?

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, true,
                extent, GridOrigin.LOWER_LEFT);

            var upscaleCoords = new Coord(5, 2, 3);
            Assert.AreEqual(tile, gpkg.GetCorrespondingTile(upscaleCoords, true));
            this._gpkgUtilsMock.Verify(utils =>
                utils.GetLastTile(new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 }, upscaleCoords));
            //TODO: add conversion specific validation after mocking origin conversion
            this.VerifyAll();
        }

        #endregion

        #region UpdateTiles

        [TestMethod]
        [TestCategory("UpdateTiles")]
        [DataRow(10, false)]
        [DataRow(100, true)]
        public void UpdateTilesWithoutConversions(int batchSize, bool isBase)
        {
            this.SetupRequiredBaseMocks(isBase);
            this._gpkgUtilsMock.Setup(utils => utils.InsertTiles(It.IsAny<IEnumerable<Tile>>()));
            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, false,
                extent, GridOrigin.UPPER_LEFT);
            var testTiles = new Tile[] { new Tile(1, 2, 3, new byte[] { }), new Tile(7, 7, 7, new byte[] { }) };

            gpkg.UpdateTiles(testTiles);
            var expectedTiles = testTiles;

            this._gpkgUtilsMock.Verify(utils =>
                utils.InsertTiles(It.Is<IEnumerable<Tile>>(tiles => tiles.SequenceEqual(expectedTiles))));
            this.VerifyAll();
        }

        [TestMethod]
        [TestCategory("UpdateTiles")]
        [DataRow(10, false)]
        [DataRow(100, true)]
        public void UpdateTilesWithConversions(int batchSize, bool isBase)
        {
            this.SetupRequiredBaseMocks(isBase);
            this._oneXOneConvertorMock
                .Setup(converter => converter.TryFromTwoXOne(It.IsAny<Tile>()))
                .Returns<Tile>(tile => tile.Z != 7 ? tile : null);
            this._gpkgUtilsMock
                .Setup(utils => utils.InsertTiles(It.IsAny<IEnumerable<Tile>>()))
                .Callback<IEnumerable<Tile>>(tiles => { tiles.ToArray(); }); // force enumerate 
            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, true,
                extent, GridOrigin.LOWER_LEFT);
            var testTiles = new Tile[]
            {
                new Tile(1, 2, 3, new byte[] { }), new Tile(7, 7, 7, new byte[] { }),
                new Tile(2, 2, 3, new byte[] { })
            };

            gpkg.UpdateTiles(testTiles);
            var expectedTiles = new Tile[] { testTiles[0], testTiles[2] };

            this._oneXOneConvertorMock.Verify(
                converter => converter.TryFromTwoXOne(It.Is<Tile>(t => t.Z == 1 && t.X == 2)),
                Times.Once); //TODO: replace expected values and check y when origin conversion is mocked
            this._oneXOneConvertorMock.Verify(
                converter => converter.TryFromTwoXOne(It.Is<Tile>(t => t.Z == 7 && t.X == 7)),
                Times.Once); //TODO: replace expected values and check y when origin conversion is mocked
            this._oneXOneConvertorMock.Verify(
                converter => converter.TryFromTwoXOne(It.Is<Tile>(t => t.Z == 2 && t.X == 2)),
                Times.Once); //TODO: replace expected values and check y when origin conversion is mocked

            Func<Tile, Tile, bool>
                compFunc = (tile1, tile2) =>
                    tile1?.Z == tile2?.Z &&
                    tile1?.X == tile2?.X; //TODO: replace expected values and check y when origin conversion is mocked
            this._gpkgUtilsMock.Verify(utils =>
                utils.InsertTiles(It.Is<IEnumerable<Tile>>(tiles => tiles.SequenceEqual(expectedTiles,
                    EqualityComparerFactory.Create<Tile>(compFunc)))));
            this.VerifyAll();
        }

        #endregion

        #region Wrapup

        public static IEnumerable<object[]> GenWrapupParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { true, false }, //vacuum
                new object[] { true, false }, //is base
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT } //origin
            );
        }

        [TestMethod]
        [TestCategory("Wrapup")]
        [DynamicData(nameof(GenWrapupParams), DynamicDataSourceType.Method)]
        public void Wrapup(bool isOneXOne, bool vacuum, bool isBase, GridOrigin origin)
        {
            this.SetupRequiredBaseMocks(isBase);
            var seq = new MockSequence();
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.CreateTileIndex());
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.UpdateTileMatrixTable(isOneXOne));
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.CreateTileCacheValidationTriggers());
            this._configurationManagerMock.InSequence(seq)
                .Setup(config => config.GetConfiguration<bool>("GPKG", "vacuum"))
                .Returns(vacuum);
            if (vacuum)
            {
                this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Vacuum());
            }


            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, isBase, isOneXOne,
                extent, origin);

            gpkg.Wrapup();
            this._gpkgUtilsMock.Verify(utils => utils.CreateTileIndex(), Times.Once);
            this._gpkgUtilsMock.Verify(utils => utils.UpdateTileMatrixTable(isOneXOne), Times.Once);
            this._gpkgUtilsMock.Verify(utils => utils.CreateTileCacheValidationTriggers(), Times.Once);
            if (vacuum)
            {
                this._gpkgUtilsMock.Verify(utils => utils.Vacuum(), Times.Once);
            }

            this.VerifyAll();
        }

        #endregion

        #region Exists

        public static IEnumerable<object[]> GenExistParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { true, false }, //is base
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { true, false } //file exists
            );
        }

        [TestMethod]
        [TestCategory("Exists")]
        [DynamicData(nameof(GenExistParams), DynamicDataSourceType.Method)]
        public void Exists(bool isOneXOne, bool isBase, GridOrigin origin, bool exist)
        {
            this.SetupRequiredBaseMocks(isBase);
            this._gpkgUtilsMock.Setup(utils => utils.Exist()).Returns(exist);
            if (isBase)
            {
                if (!exist)
                {
                    this._gpkgUtilsMock.Setup(utils => utils.Create(It.IsAny<Extent>(), isOneXOne));
                }
                else
                {
                    this._gpkgUtilsMock.Setup(utils => utils.DeleteTileTableTriggers());
                }

                this._gpkgUtilsMock.Setup(utils => utils.UpdateExtent(It.IsAny<Extent>()));
            }

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, isBase, isOneXOne,
                extent, origin);

            Assert.AreEqual(exist, gpkg.Exists());

            this._gpkgUtilsMock.Verify(utils => utils.Exist(), Times.Exactly(isBase ? 2 : 1));
            this.VerifyAll();
        }

        #endregion

        #region TileCount

        public static IEnumerable<object[]> GenTileCountParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { true, false }, //is base
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { 7, 1365 } //tile count
            );
        }

        [TestMethod]
        [TestCategory("TileCount")]
        [DynamicData(nameof(GenTileCountParams), DynamicDataSourceType.Method)]
        public void TileCount(bool isOneXOne, bool isBase, GridOrigin origin, int tileCount)
        {
            this.SetupRequiredBaseMocks(isBase);
            this._gpkgUtilsMock.Setup(utils => utils.GetTileCount()).Returns(tileCount);

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, isBase, isOneXOne,
                extent, origin);

            Assert.AreEqual(tileCount, gpkg.TileCount());
            this._gpkgUtilsMock.Verify(utils => utils.GetTileCount(), Times.Once);
            this.VerifyAll();
        }

        #endregion

        #region SetBatchIdentifier

        public static IEnumerable<object[]> GenSetBatchIdentifierParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { true, false }, //is base
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { 7, 13650 } //batch offset
            );
        }

        [TestMethod]
        [TestCategory("SetBatchIdentifier")]
        [DynamicData(nameof(GenSetBatchIdentifierParams), DynamicDataSourceType.Method)]
        public void SetBatchIdentifier(bool isOneXOne, bool isBase, GridOrigin origin, int offset)
        {
            this.SetupRequiredBaseMocks(isBase);
            this._gpkgUtilsMock.Setup(utils => utils.GetBatch(10, It.IsAny<int>()))
                .Returns(new List<Tile>());

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, isBase, isOneXOne,
                extent, origin);

            string testIdentifier = offset.ToString();
            gpkg.setBatchIdentifier(testIdentifier);
            gpkg.GetNextBatch(out string batchIdentifier);
            Assert.AreEqual(testIdentifier, batchIdentifier);
            this._gpkgUtilsMock.Verify(utils => utils.GetBatch(10, offset), Times.Once);
            this.VerifyAll();
        }

        #endregion

        #region Reset

        public static IEnumerable<object[]> GenResetParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { true, false }, //is base
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { 7, 1365 } //batch size
            );
        }

        [TestMethod]
        [TestCategory("Reset")]
        [DynamicData(nameof(GenResetParams), DynamicDataSourceType.Method)]
        public void Reset(bool isOneXOne, bool isBase, GridOrigin origin, int batchSize)
        {
            this.SetupRequiredBaseMocks(isBase);
            this._gpkgUtilsMock.Setup(utils => utils.GetBatch(batchSize, It.IsAny<int>()))
                .Returns(new List<Tile> { new Tile(0, 0, 0, new byte[] { }) });
            if (isOneXOne)
            {
                this._oneXOneConvertorMock.Setup(converter =>
                        converter.TryToTwoXOne(It.IsAny<Tile>()))
                    .Returns<Tile>(tile => tile);
            }

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, isOneXOne,
                extent, origin);

            gpkg.GetNextBatch(out string batchIdentifier);
            gpkg.GetNextBatch(out batchIdentifier);
            Assert.AreNotEqual("0", batchIdentifier);
            gpkg.Reset();
            gpkg.GetNextBatch(out batchIdentifier);
            Assert.AreEqual("0", batchIdentifier);
            this._gpkgUtilsMock.Verify(utils => utils.GetBatch(batchSize, 0), Times.Exactly(2));
            this._gpkgUtilsMock.Verify(utils => utils.GetBatch(batchSize, It.IsAny<int>()), Times.Exactly(3));
            this.VerifyAll();
        }

        #endregion

        #region GetNextBatch

        public static IEnumerable<object[]> GenGetNextBatchParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { true, false }, //is base
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { 1, 2, 10 } //tile count
            );
        }

        [TestMethod]
        [TestCategory("GetNextBatch")]
        [DynamicData(nameof(GenGetNextBatchParams), DynamicDataSourceType.Method)]
        public void GetNextBatch(bool isOneXOne, bool isBase, GridOrigin origin, int batchSize)
        {
            var tiles = new Tile[]
            {
                new Tile(0, 0, 0, new byte[] { }), new Tile(1, 1, 1, new byte[] { }),
                new Tile(2, 2, 2, new byte[] { }), new Tile(3, 3, 3, new byte[] { }),
            };
            var tileBatches = tiles.Chunk(batchSize).ToList();
            var batchIdx = 0;
            this.SetupRequiredBaseMocks(isBase);
            var seq = new MockSequence();
            for (var i = 0; i < tileBatches.Count; i++)
            {
                this._gpkgUtilsMock
                    .InSequence(seq)
                    .Setup(utils => utils.GetBatch(batchSize, It.IsAny<int>()))
                    .Returns(tileBatches[i].ToList());
                if (isOneXOne)
                {
                    for (var j = 0; j < tileBatches[i].Length; j++)
                    {
                        this._oneXOneConvertorMock
                            .InSequence(seq)
                            .Setup(converter => converter.TryToTwoXOne(It.IsAny<Tile>()))
                            .Returns<Tile>(tile => tile.Z != 0 ? tile : null);
                    }
                }
            }

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, isOneXOne,
                extent, origin);


            for (int i = 0; i < tileBatches.Count; i++)
            {
                var exactedBatch = tileBatches[i].Where(t => !isOneXOne || t.Z != 0);
                var res = gpkg.GetNextBatch(out string batchIdentifier);

                Assert.IsTrue(res.SequenceEqual(exactedBatch, EqualityComparerFactory.Create<Tile>(
                    (t1, t2) => t1.Z == t2.Z && t1.X == t2.X))); //TODO: add y after origin conversion mock
                string expectedBatchId = Math.Min(i * batchSize, tiles.Length).ToString();
                Assert.AreEqual(expectedBatchId, batchIdentifier);
                this._gpkgUtilsMock.Verify(utils => utils.GetBatch(batchSize, i * batchSize), Times.Once);
                foreach (var tile in tileBatches[i])
                {
                    //TODO: validate origin conversion
                    if (isOneXOne)
                    {
                        this._oneXOneConvertorMock.Verify(converter => converter.TryToTwoXOne(It.Is<Tile>(
                                t => t.Z == tile.Z && t.X == tile.X)),
                            Times.Once); //TODO: add y after origin conversion mock
                    }
                }
            }

            this.VerifyAll();
        }

        #endregion

        #region gpkgCreation

        public static IEnumerable<object[]> GenGpkgCreationParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT } //origin
            );
        }

        [TestMethod]
        [TestCategory("gpkgCreation")]
        [DynamicData(nameof(GenGpkgCreationParams), DynamicDataSourceType.Method)]
        public void GpkgCreation(bool isOneXOne, GridOrigin origin)
        {
            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var seq = new MockSequence();
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Exist()).Returns(false);
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Create(extent, isOneXOne));
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.UpdateExtent(extent));

            new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, true, isOneXOne,
                extent, origin);
            this._gpkgUtilsMock.Verify(utils => utils.Create(extent, isOneXOne), Times.Once);
            this._gpkgUtilsMock.Verify(utils => utils.Exist(), Times.Once);
            this._gpkgUtilsMock.Verify(utils => utils.UpdateExtent(extent), Times.Once);
            this.VerifyAll();
        }

        [TestMethod]
        [TestCategory("gpkgCreation")]
        [DynamicData(nameof(GenGpkgCreationParams), DynamicDataSourceType.Method)]
        public void GpkgCreationThrowWithoutExtent(bool isOneXOne, GridOrigin origin)
        {
            Extent? extent = null;
            Assert.ThrowsException<Exception>(() =>
                new Gpkg(this._configurationManagerMock.Object,
                    this._serviceProviderMock.Object, "test.gpkg", 10, true, isOneXOne,
                    extent, origin));
            this.VerifyAll();
        }

        #endregion

        #region helper

        private void SetupRequiredBaseMocks(bool isBase)
        {
            if (!isBase)
            {
                return;
            }

            var seq = new MockSequence();
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Exist()).Returns(true);
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.DeleteTileTableTriggers());
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.UpdateExtent(It.IsAny<Extent>()));
        }

        private void VerifyAll()
        {
            this._gpkgUtilsMock.VerifyAll();
            this._oneXOneConvertorMock.VerifyAll();
            this._configurationManagerMock.VerifyAll();
        }

        #endregion
    }
}
