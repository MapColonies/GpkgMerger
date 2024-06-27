using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.DataTypes;
using MergerLogic.Monitoring.Metrics;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MergerLogicUnitTests.DataTypes
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("XYZ")]
    [TestCategory("XYZDataSource")]
    [TestCategory("HttpDataSource")]
    [DeploymentItem(@"../../../DataTypes/TestImages")]

    public class XYZTest
    {
        #region mocks

        private MockRepository _repository;
        private Mock<IServiceProvider> _serviceProviderMock;
        private Mock<IOneXOneConvertor> _oneXOneConvertorMock;
        private Mock<IUtilsFactory> _utilsFactoryMock;
        private Mock<IHttpSourceClient> _httpUtilsMock;
        private Mock<IGeoUtils> _geoUtilsMock;
        private Mock<ILoggerFactory> _loggerFactoryMock;
        private Mock<ILogger<FS>> _loggerMock;
        private Mock<IMetricsProvider> _metricsProviderMock;
        private byte[] _jpegImageData;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._oneXOneConvertorMock = this._repository.Create<IOneXOneConvertor>();
            this._httpUtilsMock = this._repository.Create<IHttpSourceClient>();
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();
            this._utilsFactoryMock = this._repository.Create<IUtilsFactory>();
            this._metricsProviderMock = this._repository.Create<IMetricsProvider>(MockBehavior.Loose);

            this._utilsFactoryMock.Setup(factory => factory.GetDataUtils<IHttpSourceClient>(It.IsAny<string>()))
                .Returns(this._httpUtilsMock.Object);
            this._loggerMock = this._repository.Create<ILogger<FS>>(MockBehavior.Loose);
            this._loggerFactoryMock = this._repository.Create<ILoggerFactory>();
            this._loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(this._loggerMock.Object);
            this._serviceProviderMock = this._repository.Create<IServiceProvider>();
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IOneXOneConvertor)))
                .Returns(this._oneXOneConvertorMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IUtilsFactory)))
                .Returns(this._utilsFactoryMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IGeoUtils)))
                .Returns(this._geoUtilsMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(ILoggerFactory)))
                .Returns(this._loggerFactoryMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IMetricsProvider)))
                .Returns(this._metricsProviderMock.Object);
            this._jpegImageData = File.ReadAllBytes("no_transparency.jpeg");
        }

        #region TileExists

        public static IEnumerable<object[]> GenTileExistsParams()
        {
            return DynamicDataGenerator.GeneratePrams(
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
        public void TileExists(Coord cords, bool isOneXOne, GridOrigin origin, bool useCoords)
        {
            this.SetupConstructorRequiredMocks();
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
                this._httpUtilsMock
                    .InSequence(seq)
                    .Setup(utils => utils.TileExists(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => z == 2);
            }

            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var xyzSource = new XYZ(this._serviceProviderMock.Object, "test", 10, extent, grid, origin, 21, 0);

            var expected = cords.Z == 2;
            if (useCoords)
            {
                Assert.AreEqual(expected, xyzSource.TileExists(cords));
            }
            else
            {
                var tile = new Tile(cords, this._jpegImageData);
                Assert.AreEqual(expected, xyzSource.TileExists(tile));
            }
            this._httpUtilsMock.Verify(util => util.TileExists(cords.Z, cords.X, cords.Y),
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
        [DataRow(10, 2, 2, 3, false)]
        [DataRow(100, 2, 2, 3, false)]
        //missing tile
        [DataRow(10, 1, 2, 3, true)]
        [DataRow(100, 1, 2, 3, true)]
        public void GetCorrespondingTileWithoutUpscaleWithoutConversion(int batchSize, int z, int x, int y, bool expectedNull)
        {
            this.SetupConstructorRequiredMocks();
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, this._jpegImageData);
            this._httpUtilsMock.Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);

            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            var xyzSource = new XYZ(this._serviceProviderMock.Object, "test", batchSize, extent, Grid.TwoXOne, GridOrigin.LOWER_LEFT, 21, 0);

            var cords = new Coord(z, x, y);
            Assert.AreEqual(expectedNull ? null : existingTile, xyzSource.GetCorrespondingTile(cords, false));
            this._httpUtilsMock.Verify(util => util.GetTile(z, x, y), Times.Once);
            this.VerifyAll();
        }

        public static IEnumerable<object[]> GenGetCorrespondingTileWithoutUpscaleParams()
        {
            return DynamicDataGenerator.GeneratePrams(
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
        public void GetCorrespondingTileWithoutUpscale(Coord cords, bool enableUpscale, bool isOneXOne,
            GridOrigin origin)
        {
            bool expectedNull = cords.Z != 2;
            this.SetupConstructorRequiredMocks();
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, this._jpegImageData);
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
                    this._httpUtilsMock
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
                this._httpUtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);
            }

            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var xyzSource = new XYZ(this._serviceProviderMock.Object, "test", 10, extent, grid, origin, 21, 0);

            var res = xyzSource.GetCorrespondingTile(cords, enableUpscale);
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
                    this._httpUtilsMock.Verify(util => util.GetTile(It.Is<Coord>(C => C.Z == cords.Z && C.X == cords.X && C.Y == cords.Y)), Times.Once);
                }
                if (cords.Z == 2)
                {
                    this._oneXOneConvertorMock.Verify(converter => converter.ToTwoXOne(existingTile), Times.Once);
                }
            }
            else
            {
                this._httpUtilsMock.Verify(utils => utils.GetTile(cords.Z, cords.X, cords.Y));
            }
            this.VerifyAll();
        }


        public static IEnumerable<object[]> GenGetCorrespondingTileWithUpscaleOneXOneParams()
        {
            return DynamicDataGenerator.GeneratePrams(

                new object[] { true }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { true, false } //is valid conversion 
            );
        }
        public static IEnumerable<object[]> GenGetCorrespondingTileWithUpscaleTwoXOneParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { true } //is valid conversion 
            );
        }

        [TestMethod]
        [TestCategory("GetCorrespondingTile")]
        [DynamicData(nameof(GenGetCorrespondingTileWithUpscaleOneXOneParams), DynamicDataSourceType.Method)]
        [DynamicData(nameof(GenGetCorrespondingTileWithUpscaleTwoXOneParams), DynamicDataSourceType.Method)]
        public void GetCorrespondingTileWithUpscale(bool isOneXOne, GridOrigin origin, bool isValidConversion)
        {
            this.SetupConstructorRequiredMocks();
            Tile nullTile = null;
            var tile = new Tile(2, 2, 3, this._jpegImageData);
            var sequence = new MockSequence();

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
                    this._httpUtilsMock
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
                this._httpUtilsMock
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

            for (int i = 0; i < 5; i++)
            {
                this._httpUtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns(i == 4 ? tile : nullTile);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock
                    .InSequence(sequence)
                    .Setup(converter => converter.ToTwoXOne(tile))
                    .Returns<Tile>(tile => tile);
            }

            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var xyzSource = new XYZ(this._serviceProviderMock.Object, "test", 10, extent, grid, origin, 21, 0);
            var upscaleCords = new Coord(5, 2, 3);

            var expectedTile = isValidConversion ? tile : null;
            var expectedCallsAfterConversion = isValidConversion ? Times.Once() : Times.Never();
            Assert.AreEqual(expectedTile, xyzSource.GetCorrespondingTile(upscaleCords, true));
            if (origin != GridOrigin.LOWER_LEFT)
            {
                this._geoUtilsMock.Verify(utils => utils.FlipY(5, 3), Times.Once);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock.Verify(converter =>
                    converter.TryFromTwoXOne(5, 2, 3), Times.Once);
                this._httpUtilsMock.Verify(utils => utils.GetTile(It.IsAny<Coord>()), expectedCallsAfterConversion);
                this._oneXOneConvertorMock.Verify(converter => converter.TryFromTwoXOne(It.IsAny<Coord>()), Times.Once);
            }
            else
            {
                this._httpUtilsMock.Verify(utils => utils.GetTile(5, 2, 3), Times.Once);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock.Verify(converter => converter.ToTwoXOne(tile), expectedCallsAfterConversion);
            }


            this._httpUtilsMock.Verify(utils => utils.GetTile(4, 1, 1), expectedCallsAfterConversion);
            this._httpUtilsMock.Verify(utils => utils.GetTile(3, 0, 0), expectedCallsAfterConversion);
            this._httpUtilsMock.Verify(utils => utils.GetTile(2, 0, 0), expectedCallsAfterConversion);
            this._httpUtilsMock.Verify(utils => utils.GetTile(1, 0, 0), expectedCallsAfterConversion);
            this._httpUtilsMock.Verify(utils => utils.GetTile(0, 0, 0), expectedCallsAfterConversion);

            this.VerifyAll();
        }

        #endregion

        #region UpdateTiles
        public static IEnumerable<object[]> GenUpdateTilesParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { false, true }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT } //origin
            );
        }

        [TestMethod]
        [TestCategory("UpdateTiles")]
        [DynamicData(nameof(GenUpdateTilesParams), DynamicDataSourceType.Method)]
        public void UpdateTiles(bool isOneXOne, GridOrigin origin)
        {
            this.SetupConstructorRequiredMocks();

            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var xyzSource = new XYZ(this._serviceProviderMock.Object, "test", 10, extent, grid, origin, 21, 0);

            var testTiles = new Tile[]
            {
                    new Tile(1, 2, 3, this._jpegImageData), new Tile(7, 7, 7, this._jpegImageData),
                    new Tile(2, 2, 3, this._jpegImageData)
            };

            Assert.ThrowsException<NotImplementedException>(() => xyzSource.UpdateTiles(testTiles));
            this.VerifyAll();
        }

        #endregion


        #region Exists

        public static IEnumerable<object[]> GenExistParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { true } //file exists - none existing 
            );
        }

        [TestMethod]
        [TestCategory("Exists")]
        [DynamicData(nameof(GenExistParams), DynamicDataSourceType.Method)]
        public void Exists(bool isOneXOne, GridOrigin origin, bool exist)
        {
            this.SetupConstructorRequiredMocks();


            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var xyzSource = new XYZ(this._serviceProviderMock.Object, "test", 10, extent, grid, origin, 21, 0);

            Assert.AreEqual(exist, xyzSource.Exists());

            this.VerifyAll();
        }

        #endregion

        #region TileCount

        public static IEnumerable<object[]> GenTileCountParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT } //origin
            );
        }

        [TestMethod]
        [TestCategory("TileCount")]
        [DynamicData(nameof(GenTileCountParams), DynamicDataSourceType.Method)]
        public void TileCount(bool isOneXOne, GridOrigin origin)
        {
            var seq = new MockSequence();
            this._geoUtilsMock
                .InSequence(seq)
                .Setup(utils => utils.ExtentToTileRange(It.IsAny<Extent>(), 0, It.IsAny<GridOrigin>()))
                .Returns(new TileBounds(0, 0, 1, 0, 1));
            this._geoUtilsMock
                .InSequence(seq)
                .Setup(utils => utils.ExtentToTileRange(It.IsAny<Extent>(), 1, It.IsAny<GridOrigin>()))
                .Returns(new TileBounds(1, 0, 1, 0, 1));
            this._geoUtilsMock
                .InSequence(seq)
                .Setup(utils => utils.ExtentToTileRange(It.IsAny<Extent>(), 2, It.IsAny<GridOrigin>()))
                .Returns(new TileBounds(2, 0, 2, 0, 1));
            this._geoUtilsMock
                .InSequence(seq)
                .Setup(utils => utils.ExtentToTileRange(It.IsAny<Extent>(), 3, It.IsAny<GridOrigin>()))
                .Returns(new TileBounds(3, 0, 3, 0, 2));

            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var xyzSource = new XYZ(this._serviceProviderMock.Object, "test", 10, extent, grid, origin, 3, 0);

            Assert.AreEqual(10, xyzSource.TileCount());
            this._geoUtilsMock.Verify(utils => utils.ExtentToTileRange(It.IsAny<Extent>(), It.IsAny<int>(), It.IsAny<GridOrigin>()), Times.Exactly(4));
            this.VerifyAll();
        }

        #endregion

        #region SetBatchIdentifier

        public static IEnumerable<object[]> GenSetBatchIdentifierParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { 3, 5 } //batch offset
            );
        }

        [TestMethod]
        [TestCategory("SetBatchIdentifier")]
        [DynamicData(nameof(GenSetBatchIdentifierParams), DynamicDataSourceType.Method)]
        public void SetBatchIdentifier(bool isOneXOne, GridOrigin origin, int offset)
        {
            this.SetupConstructorRequiredMocks();

            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var xyzSource = new XYZ(this._serviceProviderMock.Object, "test", 10, extent, grid, origin, 21, 0);

            string testIdentifier = offset.ToString();
            xyzSource.setBatchIdentifier(testIdentifier);
            xyzSource.GetNextBatch(out string batchIdentifier, out string? _, null);
            Assert.AreEqual(testIdentifier, batchIdentifier);

            this.VerifyAll();
        }

        #endregion

        #region Reset

        public static IEnumerable<object[]> GenResetParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { 1, 2 } //batch size
            );
        }

        [TestMethod]
        [TestCategory("Reset")]
        [DynamicData(nameof(GenResetParams), DynamicDataSourceType.Method)]
        public void Reset(bool isOneXOne, GridOrigin origin, int batchSize)
        {
            this._geoUtilsMock
                .Setup(utils => utils.ExtentToTileRange(It.IsAny<Extent>(), It.IsAny<int>(), It.IsAny<GridOrigin>()))
                .Returns<Extent, int, GridOrigin>((extent, zoom, origin) => new TileBounds(zoom, 0, 1, 0, 1));

            if (origin != GridOrigin.LOWER_LEFT)
            {
                this._geoUtilsMock
                    .Setup(utils => utils.FlipY(It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int>((_, y) => y);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock
                    .Setup(converter => converter.ToTwoXOne(It.IsAny<Tile>()))
                    .Returns<Tile>(t => t);
                this._httpUtilsMock
                    .Setup(utils => utils.GetTile(It.IsAny<Coord>()))
                    .Returns(new Tile(0, 0, 0, this._jpegImageData));
                this._oneXOneConvertorMock
                    .Setup(converter => converter.TryFromTwoXOne(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => new Coord(z, x, y));
            }
            else
            {
                this._httpUtilsMock
                    .Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns(new Tile(0, 0, 0, this._jpegImageData));
            }

            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var xyzSource = new XYZ(this._serviceProviderMock.Object, "test", batchSize, extent, grid, origin, 21, 0);

            xyzSource.GetNextBatch(out string batchIdentifier, out string? _, null);
            xyzSource.GetNextBatch(out batchIdentifier, out string? _, null);
            Assert.AreNotEqual("0", batchIdentifier);
            xyzSource.Reset();
            xyzSource.GetNextBatch(out batchIdentifier, out string? _, null);
            Assert.AreEqual("0", batchIdentifier);
            this.VerifyAll();
        }

        #endregion

        #region GetNextBatch

        public static IEnumerable<object[]> GenGetNextBatchParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { 1, 2, 10 } //tile count
            );
        }

        [TestMethod]
        [TestCategory("GetNextBatch")]
        [DynamicData(nameof(GenGetNextBatchParams), DynamicDataSourceType.Method)]
        public void GetNextBatch(bool isOneXOne, GridOrigin origin, int batchSize)
        {
            int minZoom = 0;
            int maxZoom = 4;
            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            // z = 0 is invalid conversion tile z = 2 is missing tile 
            var tiles = new Tile?[]
            {
                new Tile(0, 0, 0, this._jpegImageData), new Tile(1, 0, 0, this._jpegImageData), null,
                new Tile(3, 0, 0, this._jpegImageData), new Tile(4, 0, 0, this._jpegImageData),
            };
            var tileBatches = tiles.Where(t => t is not null && (!isOneXOne || t.Z != 0)).Chunk(batchSize).ToList();
            var seq = new MockSequence();

            for (var i = minZoom; i <= maxZoom; i++)
            {
                this._geoUtilsMock
                    .InSequence(seq)
                    .Setup(utils =>
                        utils.ExtentToTileRange(extent, i, origin))
                    .Returns<Extent, int, GridOrigin>((_, zoom, _) => new TileBounds(zoom, 0, 1, 0, 1));
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                if (origin != GridOrigin.LOWER_LEFT)
                {
                    this._geoUtilsMock
                        .InSequence(seq)
                        .Setup(converter => converter.FlipY(It.IsAny<int>(), It.IsAny<int>()))
                        .Returns<int, int>((_, y) => y);
                }

                if (isOneXOne)
                {
                    this._oneXOneConvertorMock
                        .InSequence(seq)
                        .Setup(converter => converter.TryFromTwoXOne(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                        .Returns<int, int, int>((z, x, y) => z != 0 ? new Coord(z, x, y) : null);
                    if (i != 0)
                    {
                        this._httpUtilsMock
                        .InSequence(seq)
                        .Setup(utils => utils.GetTile(It.IsAny<Coord>()))
                        .Returns<Coord>(cords => cords.Z < tiles.Length ? tiles[cords.Z] : null);
                        if (i != 2)
                        {
                            this._oneXOneConvertorMock
                                .InSequence(seq)
                                .Setup(converter => converter.ToTwoXOne(It.IsAny<Tile>()))
                                .Returns<Tile>(t => t.Z != 0 ? t : null);
                        }
                    }
                }
                else
                {
                    this._httpUtilsMock
                        .InSequence(seq)
                        .Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                        .Returns<int, int, int>((z, x, y) => z < tiles.Length ? tiles[z] : null);
                }
            }

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var xyzSource = new XYZ(this._serviceProviderMock.Object, "test", batchSize, extent, grid, origin, maxZoom, minZoom);

            var comparer = ComparerFactory.Create<Tile>((t1, t2) => t1?.Z == t2?.Z && t1?.X == t2?.X && t1?.Y == t2?.Y ? 0 : -1);
            for (int i = 0; i < tileBatches.Count; i++)
            {
                var exactedBatch = tileBatches[i];
                var res = xyzSource.GetNextBatch(out string batchIdentifier, out string? _, null);

                CollectionAssert.AreEqual(exactedBatch.ToArray(), res, comparer);
                string expectedBatchId = Math.Min(i * batchSize, tiles.Length).ToString();
                Assert.AreEqual(expectedBatchId, batchIdentifier);
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                if (origin != GridOrigin.LOWER_LEFT)
                {
                    this._geoUtilsMock.Verify(converter => converter.FlipY(i, 0), Times.Once);
                }

                if (isOneXOne)
                {
                    this._oneXOneConvertorMock.Verify(converter => converter.TryFromTwoXOne(1, 0, 0), Times.Once);
                    if (i != 0)
                    {
                        this._httpUtilsMock.Verify(utils => utils.GetTile(It.Is<Coord>(c => c.Z == i && c.X == 0 && c.Y == 0)), Times.Once);
                        if (i != 2)
                        {
                            this._oneXOneConvertorMock.Verify(converter =>
                                converter.ToTwoXOne(It.Is<Tile>(t => t.Z == i && t.X == 0 && t.Y == 0)), Times.Once);
                        }
                    }
                }
                else
                {
                    this._httpUtilsMock.Verify(utils => utils.GetTile(i, 0, 0), Times.Once);
                }
            }

            this._geoUtilsMock.Verify(
                utils => utils.ExtentToTileRange(It.IsAny<Extent>(), It.IsAny<int>(), It.IsAny<GridOrigin>()),
                Times.Exactly(maxZoom - minZoom + 1));

            if (origin != GridOrigin.LOWER_LEFT)
            {
                this._geoUtilsMock.Verify(converter => converter.FlipY(It.IsAny<int>(), It.IsAny<int>()),
                    Times.Exactly(tiles.Length));
            }

            this.VerifyAll();
        }

        #endregion

        #region helper

        private void SetupConstructorRequiredMocks(MockSequence? sequence = null)
        {
            //note that adding sequence requires looping this definition for every zoom level
            this._geoUtilsMock
                .Setup(utils => utils.ExtentToTileRange(It.IsAny<Extent>(), It.IsAny<int>(), It.IsAny<GridOrigin>()))
                .Returns<Extent, int, GridOrigin>((extent, zoom, origin) => new TileBounds(zoom, 0, 0, 0, 0));
        }

        private void VerifyAll()
        {
            this._httpUtilsMock.VerifyAll();
            this._oneXOneConvertorMock.VerifyAll();
            this._geoUtilsMock.VerifyAll();
        }

        #endregion
    }
}
