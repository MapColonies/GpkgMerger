using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
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
        private Mock<IGpkgClient> _gpkgUtilsMock;
        private Mock<IGeoUtils> _geoUtilsMock;
        private Mock<ILoggerFactory> _loggerFactoryMock;
        private Mock<ILogger<Gpkg>> _loggerMock;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._configurationManagerMock = this._repository.Create<IConfigurationManager>();
            this._oneXOneConvertorMock = this._repository.Create<IOneXOneConvertor>();
            this._gpkgUtilsMock = this._repository.Create<IGpkgClient>();
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();
            this._utilsFactoryMock = this._repository.Create<IUtilsFactory>();
            this._utilsFactoryMock.Setup(factory => factory.GetDataUtils<IGpkgClient>(It.IsAny<string>()))
                .Returns(this._gpkgUtilsMock.Object);
            this._loggerMock = this._repository.Create<ILogger<Gpkg>>(MockBehavior.Loose);
            this._loggerFactoryMock = this._repository.Create<ILoggerFactory>();
            this._loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(this._loggerMock.Object);
            this._serviceProviderMock = this._repository.Create<IServiceProvider>();
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IOneXOneConvertor)))
                .Returns(this._oneXOneConvertorMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IUtilsFactory)))
                .Returns(this._utilsFactoryMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(ILoggerFactory)))
                .Returns(this._loggerFactoryMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IGeoUtils)))
                .Returns(this._geoUtilsMock.Object);
        }

        #region TileExists

        public static IEnumerable<object[]> GenTileExistsParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is base
                new object[] {
                    new Coord(2,2,3), //existing tile
                    new Coord(1,2,3), //missing tile
                    new Coord(0,2,3) //invalid conversion tile

                }, //cords
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { true, false } // use cords
            );
        }

        [TestMethod]
        [TestCategory("TileExists")]
        [DynamicData(nameof(GenTileExistsParams), DynamicDataSourceType.Method)]
        public void TileExists(bool isBase, Coord cords, bool isOneXOne, GridOrigin origin, bool useCoords)
        {
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            
            this.SetupRequiredBaseMocks(isBase, isOneXOne, extent);
            var seq = new MockSequence();
            if (origin == GridOrigin.UPPER_LEFT)
            {
                this._geoUtilsMock
                    .InSequence(seq)
                    .Setup(utils => utils.FlipY(It.IsAny<Coord>()))
                    .Returns<Coord>(c => c.Y);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock
                    .InSequence(seq)
                    .Setup(converter => converter.TryFromTwoXOne(It.IsAny<Coord>()))
                    .Returns<Coord>(cords => cords.Z != 0 ? cords : null);
            }
            if (cords.Z != 0 || !isOneXOne)
            {
                this._gpkgUtilsMock
                    .InSequence(seq)
                    .Setup(utils => utils.TileExists(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => z == 2);
            }
            
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, grid, origin,
                isBase, extent);

            var expected = cords.Z == 2;
            if (useCoords)
            {
                Assert.AreEqual(expected, gpkg.TileExists(cords));
            }
            else
            {
                var tile = new Tile(cords, new byte[] { });
                Assert.AreEqual(expected, gpkg.TileExists(tile));
            }
            this._gpkgUtilsMock.Verify(util => util.TileExists(cords.Z, cords.X, cords.Y),
                cords.Z != 0 || !isOneXOne
                    ? Times.Once
                    : Times.Never);
            this._geoUtilsMock.Verify(utils => utils.FlipY(It.Is<Coord>(c => c.Z == cords.Z && c.X == cords.X && c.Y == cords.Y)),
                    origin == GridOrigin.UPPER_LEFT
                        ? Times.Once
                        : Times.Never);
            this._oneXOneConvertorMock.Verify(converter => converter.TryFromTwoXOne(It.Is<Coord>(c => c.Z == cords.Z && c.X == cords.X && c.Y == cords.Y)),
                isOneXOne
                    ? Times.Once
                    : Times.Never);
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
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            this.SetupRequiredBaseMocks(isBase, false, extent);
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            this._gpkgUtilsMock.Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);
            
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, Grid.TwoXOne,
                GridOrigin.LOWER_LEFT, isBase, extent);

            var cords = new Coord(z, x, y);
            Assert.AreEqual(expectedNull ? null : existingTile, gpkg.GetCorrespondingTile(cords, false));
            this._gpkgUtilsMock.Verify(util => util.GetTile(z, x, y), Times.Once);
            this.VerifyAll();
        }

        public static IEnumerable<object[]> GenGetCorrespondingTileWithoutUpscaleParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is base
                new object[] {
                    new Coord(2,2,3), //existing tile
                    new Coord(1,2,3), //missing tile
                    new Coord(0,2,3) //invalid conversion tile

                }, //cords
                new object[] { false }, //enable upscale
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT } //origin
            );
        }

        public static IEnumerable<object[]> GenGetCorrespondingTileWithoutUpscaleWhenEnabledParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is base
                new object[] {
                    new Coord(2,2,3), //existing tile
                }, //cords
                new object[] { true }, //enable upscale
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT } //origin
            );
        }

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        [DynamicData(nameof(GenGetCorrespondingTileWithoutUpscaleParams), DynamicDataSourceType.Method)]
        [DynamicData(nameof(GenGetCorrespondingTileWithoutUpscaleWhenEnabledParams), DynamicDataSourceType.Method)]
        public void GetCorrespondingTileWithoutUpscale(bool isBase, Coord cords, bool enableUpscale, bool isOneXOne,
            GridOrigin origin)
        {
            bool expectedNull = cords.Z != 2;
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            
            this.SetupRequiredBaseMocks(isBase, isOneXOne, extent);
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            var sequence = new MockSequence();
            if (origin != GridOrigin.LOWER_LEFT)
            {
                this._geoUtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.FlipY(It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int>((z, y) => y);
            }

            if (isOneXOne)
            {
                this._oneXOneConvertorMock
                    .InSequence(sequence)
                    .Setup(converter => converter.TryFromTwoXOne(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => z != 0 ? new Coord(z, x, y) : null);
                if (cords.Z != 0)
                {
                    this._gpkgUtilsMock
                        .InSequence(sequence)
                        .Setup(utils => utils.GetTile(It.IsAny<Coord>()))
                        .Returns<Coord>(cords => cords.Z == 2 ? existingTile : nullTile);
                }
                if (cords.Z == 2)
                {
                    this._oneXOneConvertorMock
                        .InSequence(sequence)
                        .Setup(converter => converter.ToTwoXOne(It.IsAny<Tile>()))
                        .Returns<Tile>(tile => tile.Z != 0 ? tile : null);
                }
            }
            else
            {
                this._gpkgUtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);
            }
            
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, grid, origin,
                isBase, extent);

            var res = gpkg.GetCorrespondingTile(cords, enableUpscale);
            if (expectedNull)
            {
                Assert.IsNull(res);
            }
            else
            {
                Assert.IsTrue(res.Z == 2 && res.X == 2 && res.Y == 3);
            }

            if (origin != GridOrigin.LOWER_LEFT)
            {
                this._geoUtilsMock.Verify(utils => utils.FlipY(cords.Z, cords.Y), Times.Once);
            }

            if (isOneXOne)
            {
                this._oneXOneConvertorMock.Verify(converter => converter.TryFromTwoXOne(cords.Z, cords.X, cords.Y));
                if (cords.Z != 0)
                {
                    this._gpkgUtilsMock.Verify(util => util.GetTile(It.Is<Coord>(C => C.Z == cords.Z && C.X == cords.X && C.Y == cords.Y)), Times.Once);
                }
                if (cords.Z == 2)
                {
                    this._oneXOneConvertorMock.Verify(converter => converter.ToTwoXOne(existingTile), Times.Once);
                }
            }
            else
            {
                this._gpkgUtilsMock.Verify(utils => utils.GetTile(cords.Z, cords.X, cords.Y));
            }
            this.VerifyAll();
        }


        public static IEnumerable<object[]> GenGetCorrespondingTileWithUpscaleOneXOneParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is base
                new object[] { true }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { true, false } //is valid conversion 
            );
        }
        public static IEnumerable<object[]> GenGetCorrespondingTileWithUpscaleTwoXOneParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is base
                new object[] { false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { true } //is valid conversion 
            );
        }

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        [DynamicData(nameof(GenGetCorrespondingTileWithUpscaleOneXOneParams), DynamicDataSourceType.Method)]
        [DynamicData(nameof(GenGetCorrespondingTileWithUpscaleTwoXOneParams), DynamicDataSourceType.Method)]
        public void GetCorrespondingTileWithUpscale(bool isBase, bool isOneXOne, GridOrigin origin, bool isValidConversion)
        {
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            Tile nullTile = null;
            var tile = new Tile(2, 2, 3, new byte[] { });
            var sequence = new MockSequence();
            
            this.SetupRequiredBaseMocks(isBase, isOneXOne, extent);

            if (origin != GridOrigin.LOWER_LEFT)
            {
                this._geoUtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.FlipY(5, 3))
                    .Returns<int, int>((z, y) => y);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock
                    .InSequence(sequence)
                    .Setup(converter => converter.TryFromTwoXOne(5, 2, 3))
                    .Returns<int, int, int>((z, x, y) => isValidConversion ? new Coord(z, x, y) : null);
                if (isValidConversion)
                {
                    this._gpkgUtilsMock
                        .InSequence(sequence)
                        .Setup(utils => utils.GetTile(It.Is<Coord>(c => c.Z == 5 && c.X == 2 && c.Y == 3)))
                        .Returns(nullTile);
                }

                this._oneXOneConvertorMock.InSequence(sequence)
                    .Setup(converter => converter.TryFromTwoXOne(It.Is<Coord>(c => c.Z == 5 && c.X == 2 && c.Y == 3)))
                    .Returns<Coord>(isValidConversion ? cords => cords : null);
            }
            else
            {
                this._gpkgUtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.GetTile(5, 2, 3))
                    .Returns(nullTile);
            }
            if (origin != GridOrigin.LOWER_LEFT)
            {
                this._geoUtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.FlipY(It.Is<Coord>(c => c.Z == 5 && c.X == 2 && c.Y == 3)))
                    .Returns<Coord>(c => c.Y);
            }

            this._gpkgUtilsMock
                .InSequence(sequence)
                .Setup(utils => utils.GetLastTile(It.IsAny<int[]>(), It.Is<int>(z => z == 5)))
                .Returns(tile);
            if (isOneXOne)
            {
                this._oneXOneConvertorMock
                    .InSequence(sequence)
                    .Setup(converter => converter.ToTwoXOne(tile))
                    .Returns<Tile>(tile => tile);
            }
            
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, grid, origin,
                isBase, extent);
            var upscaleCords = new Coord(5, 2, 3);

            var expectedTile = isValidConversion ? tile : null;
            Assert.AreEqual(expectedTile, gpkg.GetCorrespondingTile(upscaleCords, true));
            if (origin != GridOrigin.LOWER_LEFT)
            {
                this._geoUtilsMock.Verify(utils => utils.FlipY(5, 3), Times.Once);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock.Verify(converter =>
                    converter.TryFromTwoXOne(5, 2, 3), Times.Once);
                this._gpkgUtilsMock.Verify(utils => utils.GetTile(It.IsAny<Coord>()), isValidConversion ? Times.Once : Times.Never);
                this._oneXOneConvertorMock.Verify(converter => converter.TryFromTwoXOne(It.IsAny<Coord>()), Times.Once);
            }
            else
            {
                this._gpkgUtilsMock.Verify(utils => utils.GetTile(5, 2, 3), Times.Once);
            }
            this._gpkgUtilsMock.Verify(utils => utils.GetLastTile(new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 }, upscaleCords.Z), isValidConversion ? Times.Once : Times.Never);

            if (isOneXOne)
            {
                this._oneXOneConvertorMock.Verify(converter => converter.ToTwoXOne(tile), isValidConversion ? Times.Once : Times.Never);
            }

            this.VerifyAll();
        }

        #endregion

        #region UpdateTiles
        public static IEnumerable<object[]> GenUpdateTilesParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is base
                new object[] { false, true }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT } //origin
            );
        }

        [TestMethod]
        [TestCategory("UpdateTiles")]
        [DynamicData(nameof(GenUpdateTilesParams), DynamicDataSourceType.Method)]
        public void UpdateTiles(bool isBase, bool isOneXOne, GridOrigin origin)
        {
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            
            this.SetupRequiredBaseMocks(isBase, isOneXOne, extent);

            if (origin == GridOrigin.UPPER_LEFT)
            {
                this._geoUtilsMock
                    .Setup(utils => utils.FlipY(It.IsAny<Tile>()))
                    .Returns<Tile>(t => t.Y);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock
                    .Setup(converter => converter.TryFromTwoXOne(It.IsAny<Tile>()))
                    .Returns<Tile>(tile => tile.Z != 7 ? tile : null);
            }
            this._gpkgUtilsMock
                .Setup(utils => utils.InsertTiles(It.IsAny<IEnumerable<Tile>>()))
                .Callback<IEnumerable<Tile>>(tiles =>
                {
                    tiles.ToArray();
                }); // force enumerate 
            
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, grid, origin,
                isBase, extent);

            var testTiles = new Tile[]
            {
                new Tile(1, 2, 3, new byte[] { }), new Tile(7, 7, 7, new byte[] { }),
                new Tile(2, 2, 3, new byte[] { })
            };
            gpkg.UpdateTiles(testTiles);

            var expectedTiles = isOneXOne ? new Tile[] { testTiles[0], testTiles[2] } : testTiles;
            var tileComparer = EqualityComparerFactory.Create<Tile>((tile1, tile2) =>
                tile1?.Z == tile2?.Z &&
                tile1?.X == tile2?.X &&
                tile1?.Y == tile2?.Y);
            foreach (var tile in testTiles)
            {
                if (origin == GridOrigin.UPPER_LEFT)
                {
                    this._geoUtilsMock.Verify(utils => utils.FlipY(It.Is<Tile>(tile, tileComparer)), Times.Once);
                }
                if (isOneXOne)
                {
                    this._oneXOneConvertorMock.Verify(
                        converter => converter.TryFromTwoXOne(It.Is<Tile>(tile, tileComparer)), Times.Once);
                }
            }
            this._gpkgUtilsMock.Verify(utils =>
                utils.InsertTiles(It.Is<IEnumerable<Tile>>(tiles => tiles.SequenceEqual(expectedTiles,
                   tileComparer))), Times.Once);
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
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            
            this.SetupRequiredBaseMocks(isBase, isOneXOne, extent);
            var seq = new MockSequence();
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.UpdateTileMatrixTable(isOneXOne));
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.CreateTileCacheValidationTriggers());
            this._configurationManagerMock.InSequence(seq)
                .Setup(config => config.GetConfiguration<bool>("GPKG", "vacuum"))
                .Returns(vacuum);
            if (vacuum)
            {
                this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Vacuum());
            }
            
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, grid, origin,
                isBase, extent);

            gpkg.Wrapup();
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
            var seq = new MockSequence();
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            Extent extent = isOneXOne ?
                new Extent() { MinX = -180, MinY = -180, MaxX = 180, MaxY = 180 }
                :
                new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };

            if (!isBase)
            {
                this._geoUtilsMock.InSequence(seq).Setup(utils => utils.DefaultExtent(isOneXOne)).Returns(extent);
            }
            
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Exist()).Returns(exist);
            
            if (!exist && isBase)
            {
                this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Create(It.IsAny<Extent>(), isOneXOne));
            }
            
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.IsValidGrid(It.IsAny<bool>())).Returns(true);

            if (isBase)
            {
                this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.DeleteTileTableTriggers());
                this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.UpdateExtent(It.IsAny<Extent>()));
            }
            else
            {
                this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.GetExtent()).Returns(extent);
            }
            
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Exist()).Returns(true);

            var action = () =>
            {
                var gpkg = new Gpkg(this._configurationManagerMock.Object,
                    this._serviceProviderMock.Object, "test.gpkg", 10, grid, origin,
                    isBase, extent);
                
                Assert.AreEqual(true, gpkg.Exists());
            };

            if (!exist && !isBase)
            {
                Assert.ThrowsException<Exception>(action);
                this._gpkgUtilsMock.Verify(utils => utils.Exist(), Times.Once);
            }
            else
            {
                action();
                this._gpkgUtilsMock.Verify(utils => utils.Exist(), Times.Exactly(2));
            }

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
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            this.SetupRequiredBaseMocks(isBase, isOneXOne, extent);
            this._gpkgUtilsMock.Setup(utils => utils.GetTileCount()).Returns(tileCount);
            
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, grid, origin,
                isBase, extent);

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
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            
            this.SetupRequiredBaseMocks(isBase, isOneXOne, extent);
            this._gpkgUtilsMock.Setup(utils => utils.GetBatch(10, It.IsAny<long>()))
                .Returns(new List<Tile>());
            
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, grid, origin,
                isBase, extent);

            string testIdentifier = offset.ToString();
            gpkg.setBatchIdentifier(testIdentifier);
            gpkg.GetNextBatch(out string batchIdentifier, out string? _, null);
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
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            
            this.SetupRequiredBaseMocks(isBase, isOneXOne, extent);
            this._gpkgUtilsMock.Setup(utils => utils.GetBatch(batchSize, It.IsAny<long>()))
                .Returns(new List<Tile> { new Tile(0, 0, 0, new byte[] { }) });
            if (origin == GridOrigin.UPPER_LEFT)
            {
                this._geoUtilsMock.Setup(converter => converter.FlipY(It.IsAny<Tile>()))
                    .Returns<Tile>(t => t.Y);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock.Setup(converter =>
                        converter.TryToTwoXOne(It.IsAny<Tile>()))
                    .Returns<Tile>(tile => tile);
            }
            
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, grid, origin,
                isBase, extent);

            gpkg.GetNextBatch(out string batchIdentifier, out string? _, null);
            gpkg.GetNextBatch(out batchIdentifier, out string? _, null);
            Assert.AreNotEqual("0", batchIdentifier);
            gpkg.Reset();
            gpkg.GetNextBatch(out batchIdentifier, out string? _, null);
            Assert.AreEqual("0", batchIdentifier);
            this._gpkgUtilsMock.Verify(utils => utils.GetBatch(batchSize, 0), Times.Exactly(2));
            this._gpkgUtilsMock.Verify(utils => utils.GetBatch(batchSize, It.IsAny<long>()), Times.Exactly(3));
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
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var tiles = new Tile[]
            {
                new Tile(0, 0, 0, new byte[] { }), new Tile(1, 1, 1, new byte[] { }),
                new Tile(2, 2, 2, new byte[] { }), new Tile(3, 3, 3, new byte[] { }),
            };
            var tileBatches = tiles.Chunk(batchSize).ToList();
            var batchIdx = 0;
            
            this.SetupRequiredBaseMocks(isBase, isOneXOne, extent);
            var seq = new MockSequence();
            for (var i = 0; i < tileBatches.Count; i++)
            {
                this._gpkgUtilsMock
                    .InSequence(seq)
                    .Setup(utils => utils.GetBatch(batchSize, It.IsAny<long>()))
                    .Returns(tileBatches[i].ToList());
                for (var j = 0; j < tileBatches[i].Length; j++)
                {
                    if (origin == GridOrigin.UPPER_LEFT)
                    {
                        this._geoUtilsMock
                            .InSequence(seq)
                            .Setup(converter => converter.FlipY(It.IsAny<Tile>()))
                            .Returns<Tile>(t => t.Y);
                    }
                    if (isOneXOne)
                    {
                        this._oneXOneConvertorMock
                            .InSequence(seq)
                            .Setup(converter => converter.TryToTwoXOne(It.IsAny<Tile>()))
                            .Returns<Tile>(tile => tile.Z != 0 ? tile : null);
                    }
                }
            }
            
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, grid, origin,
                isBase, extent);

            for (int i = 0; i < tileBatches.Count; i++)
            {
                var exactedBatch = tileBatches[i].Where(t => !isOneXOne || t.Z != 0);
                var res = gpkg.GetNextBatch(out string batchIdentifier, out string? _, null);

                Assert.IsTrue(res.SequenceEqual(exactedBatch, EqualityComparerFactory.Create<Tile>(
                    (t1, t2) => t1.Z == t2.Z && t1.X == t2.X && t1.Y == t2.Y)));
                string expectedBatchId = Math.Min(i * batchSize, tiles.Length).ToString();
                Assert.AreEqual(expectedBatchId, batchIdentifier);
                this._gpkgUtilsMock.Verify(utils => utils.GetBatch(batchSize, i * batchSize), Times.Once);
                foreach (var tile in tileBatches[i])
                {
                    if (origin == GridOrigin.UPPER_LEFT)
                    {
                        this._geoUtilsMock.Verify(converter => converter.FlipY(tile), Times.Once);
                    }
                    if (isOneXOne)
                    {
                        this._oneXOneConvertorMock.Verify(converter => converter.TryToTwoXOne(It.Is<Tile>(
                                t => t.Z == tile.Z && t.X == tile.X && t.Y == tile.Y)), Times.Once);
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
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            var seq = new MockSequence();
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Exist()).Returns(false);
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Create(extent, isOneXOne));
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.IsValidGrid(isOneXOne)).Returns(true);
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.DeleteTileTableTriggers());
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.UpdateExtent(extent));

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, grid, origin,
                true, extent);
            this._gpkgUtilsMock.Verify(utils => utils.Exist(), Times.Once);
            this._gpkgUtilsMock.Verify(utils => utils.Create(extent, isOneXOne), Times.Once);
            this._gpkgUtilsMock.Verify(utils => utils.IsValidGrid(isOneXOne), Times.Once);
            this._gpkgUtilsMock.Verify(utils => utils.DeleteTileTableTriggers(), Times.Once);
            this._gpkgUtilsMock.Verify(utils => utils.UpdateExtent(extent), Times.Once);
            this.VerifyAll();
        }

        [TestMethod]
        [TestCategory("gpkgCreation")]
        [DynamicData(nameof(GenGpkgCreationParams), DynamicDataSourceType.Method)]
        public void GpkgCreationThrowWithoutExtent(bool isOneXOne, GridOrigin origin)
        {
            Extent? extent = null;
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            Assert.ThrowsException<Exception>(() =>
                new Gpkg(this._configurationManagerMock.Object,
                    this._serviceProviderMock.Object, "test.gpkg", 10, grid,
                    origin, true, extent));
            this.VerifyAll();
        }

        #endregion

        #region gridValidation
        public static IEnumerable<object[]> GenGridValidationParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT } //origin
            );
        }
        
        [TestMethod]
        [TestCategory("gridValidation")]
        [DynamicData(nameof(GenGridValidationParams), DynamicDataSourceType.Method)]
        public void GpkgSourceThrowsExceptionWhenGridIsNotValid(bool isOneXOne, GridOrigin origin)
        {
            Extent? extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };

            var seq = new MockSequence();
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Exist()).Returns(true);
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.IsValidGrid(It.IsAny<bool>())).Returns(false);

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            Assert.ThrowsException<Exception>(() =>
                new Gpkg(this._configurationManagerMock.Object,
                    this._serviceProviderMock.Object, "test.gpkg", 10, grid,
                    origin, true, extent));
            this.VerifyAll();
        }
        #endregion

        #region helper

        private void SetupRequiredBaseMocks(bool isBase, bool isOneXOne, Extent extent)
        {
            var seq = new MockSequence();
            
            if (!isBase)
            {
                this._geoUtilsMock.InSequence(seq).Setup(utils => utils.DefaultExtent(isOneXOne)).Returns(extent);
            }
            
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.Exist()).Returns(true);
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.IsValidGrid(It.IsAny<bool>())).Returns(true);

            if (!isBase)
            {
                this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.GetExtent()).Returns(extent);
                return;
            }

            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.DeleteTileTableTriggers());
            this._gpkgUtilsMock.InSequence(seq).Setup(utils => utils.UpdateExtent(It.IsAny<Extent>()));
        }

        private void VerifyAll()
        {
            this._gpkgUtilsMock.VerifyAll();
            this._oneXOneConvertorMock.VerifyAll();
            this._configurationManagerMock.VerifyAll();
            this._geoUtilsMock.VerifyAll();
        }

        #endregion
    }
}
