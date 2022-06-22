using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;

namespace MergerLogicUnitTests.DataTypes
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("gpkg")]
    [TestCategory("gpkgDataSource")]
    public class GpkgTest
    {
        #region mocks
        private Mock<IConfigurationManager> _configurationManagerMock;
        private Mock<IServiceProvider> _serviceProviderMock;
        private Mock<IOneXOneConvertor> _iOneXOneConvertorMock;
        private Mock<IUtilsFactory> _utilsFactoryMock;
        private Mock<IGpkgUtils> _gpkgUtilsMock;
        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._configurationManagerMock = new Mock<IConfigurationManager>();
            this._iOneXOneConvertorMock = new Mock<IOneXOneConvertor>();
            this._gpkgUtilsMock = new Mock<IGpkgUtils>();
            this._utilsFactoryMock = new Mock<IUtilsFactory>();
            this._utilsFactoryMock.Setup(factory => factory.GetDataUtils<IGpkgUtils>(It.IsAny<string>())).Returns(this._gpkgUtilsMock.Object);
            this._serviceProviderMock = new Mock<IServiceProvider>();
            this._serviceProviderMock.Setup(container =>
                container.GetService(typeof(IOneXOneConvertor))).Returns(this._iOneXOneConvertorMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IUtilsFactory)))
                .Returns(this._utilsFactoryMock.Object);
        }

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
        public void TileExistsWithoutConversion(int batchSize, bool isBase, int z, int x, int y, bool expected, bool useCords)
        {
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
            this._iOneXOneConvertorMock.VerifyAll();
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
        public void TileExistsWithConversion(int batchSize, bool isBase, int z, int x, int y, bool expected, bool useCords)
        {
            this._gpkgUtilsMock.Setup(utils => utils.TileExists(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z == 2);
            this._iOneXOneConvertorMock.Setup(converter => converter.TryFromTwoXOne(It.IsAny<Coord>()))
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
            this._gpkgUtilsMock.Verify(util => util.TileExists(z, x, It.IsAny<int>()), z != 0 ? Times.Once : Times.Never);//TODO: replace with specific validation after mocking origin conversion
            this._iOneXOneConvertorMock.VerifyAll();//TODO: replace with specific validation after mocking origin conversion
        }

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        //existingTile
        [DataRow(10, false, 2, 2, 3, false)]
        [DataRow(100, true, 2, 2, 3, false)]
        //missing tile
        [DataRow(10, false, 1, 2, 3, true)]
        [DataRow(100, true, 1, 2, 3, true)]
        public void GetCorrespondingTileWithoutUpscaleWithoutConversion(int batchSize, bool isBase, int z, int x, int y, bool expectedNull)
        {
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
            this._iOneXOneConvertorMock.VerifyAll();
        }

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        //existing tile
        [DataRow(10, false, 2, 2, 3, false)]
        [DataRow(100, true, 2, 2, 3, false)]
        //missing tile
        [DataRow(10, false, 1, 2, 3, false)]
        [DataRow(100, true, 1, 2, 3, false)]
        //invalid conversion tile
        [DataRow(10, false, 0, 2, 3, false)]
        [DataRow(100, true, 0, 2, 3, false)]
        public void GetCorrespondingTileWithoutUpscaleWithConversion(int batchSize, bool isBase, int z, int x, int y, bool expectedNull)
        {
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            var sequence = new MockSequence();
            this._gpkgUtilsMock.InSequence(sequence).Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);
            this._iOneXOneConvertorMock.InSequence(sequence).Setup(converter => converter.TryFromTwoXOne(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z != 0 ? new Coord(z, x, y) : null);
            this._iOneXOneConvertorMock.InSequence(sequence).Setup(converter => converter.ToTwoXOne(It.IsAny<Tile>()))
                .Returns<Tile>(tile => tile.Z != 0 ? tile : null);
            //TODO: mock origin convertor?

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, true,
                extent, GridOrigin.UPPER_LEFT);

            var cords = new Coord(z, x, y);
            Assert.AreEqual(expectedNull ? nullTile : It.IsAny<Tile>(), gpkg.GetCorrespondingTile(cords, false));//TODO: replace with specific validation after mocking origin conversion
            this._gpkgUtilsMock.Verify(util => util.GetTile(z, x, It.IsAny<int>()), Times.Once);//TODO: replace with specific validation after mocking origin conversion
            this._iOneXOneConvertorMock.VerifyAll();//TODO: replace with specific validation after mocking origin conversion

        }

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        [DataRow(10, false)]
        [DataRow(100, true)]
        public void GetCorrespondingTileWithUpscaleWithoutConversion(int batchSize, bool isBase)
        {
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            this._gpkgUtilsMock.Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).Returns(nullTile);
            this._gpkgUtilsMock.Setup(utils => utils.GetTile(2, It.IsAny<int>(), It.IsAny<int>())).Returns(existingTile);
            this._gpkgUtilsMock.Setup(utils => utils.GetLastTile(It.IsAny<int[]>(), It.IsAny<Coord>()))
                .Returns<int[], Coord>((cords, baseCords) => baseCords.z == 5 ? existingTile : null);

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, false,
                extent, GridOrigin.UPPER_LEFT);

            var existingCoords = existingTile.GetCoord();
            var notExistingCoords = new Coord(1, 2, 3);
            var upscaleCoords = new Coord(5, 2, 3);

            Assert.AreEqual(existingTile, gpkg.GetCorrespondingTile(existingCoords, true));
            Assert.AreEqual(existingTile, gpkg.GetCorrespondingTile(upscaleCoords, true));
            Assert.IsNull(gpkg.GetCorrespondingTile(notExistingCoords, true));
            this._gpkgUtilsMock.Verify(utils => utils.GetLastTile(new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 }, upscaleCoords));
            this._iOneXOneConvertorMock.VerifyAll();
        }

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        [DataRow(10, false)]
        [DataRow(100, true)]
        public void GetCorrespondingTileWithUpscaleWithConversion(int batchSize, bool isBase)
        {
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            this._gpkgUtilsMock.Setup(utils => utils.GetTile(2, It.IsAny<int>(), It.IsAny<int>())).Returns(existingTile);
            this._gpkgUtilsMock.Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).Returns(nullTile);
            this._gpkgUtilsMock.Setup(utils => utils.GetLastTile(It.IsAny<int[]>(), It.IsAny<Coord>()))
                .Returns<int[], Coord>((cords, baseCords) => baseCords.z == 5 ? existingTile : null);
            var sequence = new MockSequence();
            this._iOneXOneConvertorMock.InSequence(sequence).Setup(converter => converter.TryFromTwoXOne(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z != 0 ? new Coord(z, x, y) : null);
            this._iOneXOneConvertorMock.InSequence(sequence).Setup(converter => converter.TryFromTwoXOne(It.IsAny<Coord>()))
                .Returns<Coord>(cords => cords.z != 0 ? cords : null);
            this._iOneXOneConvertorMock.InSequence(sequence).Setup(converter => converter.ToTwoXOne(It.IsAny<Tile>()))
                .Returns<Tile>(tile => tile.Z != 0 ? tile : null);
            //TODO: mock origin convertor?

            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, true,
                extent, GridOrigin.LOWER_LEFT);

            var existingCoords = existingTile.GetCoord();
            var notExistingCoords = new Coord(1, 2, 3);
            var invalidCoords = new Coord(0, 2, 3);
            var upscaleCoords = new Coord(5, 2, 3);
            Assert.AreEqual(It.IsAny<Tile>(), gpkg.GetCorrespondingTile(existingCoords, true)); //TODO: replace with specific validation after mocking origin conversion
            Assert.AreEqual(existingTile, gpkg.GetCorrespondingTile(upscaleCoords, true)); //TODO: replace with specific validation after mocking origin conversion
            Assert.IsNull(gpkg.GetCorrespondingTile(notExistingCoords, true));
            Assert.IsNull(gpkg.GetCorrespondingTile(invalidCoords, true));
            this._gpkgUtilsMock.Verify(utils => utils.GetLastTile(new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 }, upscaleCoords));
            this._iOneXOneConvertorMock.VerifyAll();//TODO: replace with specific validation after mocking origin conversion
        }

        [TestMethod]
        [TestCategory("UpdateTiles")]
        [DataRow(10, false)]
        [DataRow(100, true)]
        public void UpdateTiles(int batchSize, bool isBase)
        {
            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", batchSize, isBase, false,
                extent, GridOrigin.UPPER_LEFT);

        }

        [TestMethod]
        [TestCategory("Wrapup")]
        public void Wrapup()
        {
            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, false, false,
                extent, GridOrigin.UPPER_LEFT);

        }

        [TestMethod]
        [TestCategory("Exists")]
        public void Exists()
        {
            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, false, false,
                extent, GridOrigin.UPPER_LEFT);

        }

        [TestMethod]
        [TestCategory("TileCount")]
        public void TileCount()
        {
            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, false, false,
                extent, GridOrigin.UPPER_LEFT);

        }

        [TestMethod]
        [TestCategory("SetBatchIdentifier")]
        public void SetBatchIdentifier()
        {
            var extent = new Extent() { minX = -180, minY = -90, maxX = 180, maxY = 90 };
            var gpkg = new Gpkg(this._configurationManagerMock.Object,
                this._serviceProviderMock.Object, "test.gpkg", 10, false, false,
                extent, GridOrigin.UPPER_LEFT);

        }
    }
}
