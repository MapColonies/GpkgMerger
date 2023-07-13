using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MergerLogicUnitTests.DataTypes
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("S3")]
    [TestCategory("S3DataSource")]
    public class S3Test
    {
        #region mocks

        private MockRepository _repository;
        private Mock<IServiceProvider> _serviceProviderMock;
        private Mock<IOneXOneConvertor> _oneXOneConvertorMock;
        private Mock<IUtilsFactory> _utilsFactoryMock;
        private Mock<IS3Client> _s3UtilsMock;
        private Mock<IGeoUtils> _geoUtilsMock;
        private Mock<IPathUtils> _pathUtilsMock;
        private Mock<ILoggerFactory> _loggerFactoryMock;
        private Mock<ILogger<S3>> _loggerMock;
        private Mock<ILogger<S3Client>> _s3ClientLoggerMock;
        private Mock<IAmazonS3> _s3ClientMock;
        private Mock<IConfigurationManager> _configurationManagerMock;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._oneXOneConvertorMock = this._repository.Create<IOneXOneConvertor>();
            this._s3UtilsMock = this._repository.Create<IS3Client>();
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();
            this._pathUtilsMock = this._repository.Create<IPathUtils>();
            this._utilsFactoryMock = this._repository.Create<IUtilsFactory>();
            this._utilsFactoryMock.Setup(factory => factory.GetDataUtils<IS3Client>(It.IsAny<string>()))
                .Returns(this._s3UtilsMock.Object);
            this._loggerMock = this._repository.Create<ILogger<S3>>(MockBehavior.Loose);
            this._s3ClientLoggerMock = this._repository.Create<ILogger<S3Client>>(MockBehavior.Loose);
            this._loggerFactoryMock = this._repository.Create<ILoggerFactory>();
            this._loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(this._loggerMock.Object);
            this._loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(this._s3ClientLoggerMock.Object);


            this._serviceProviderMock = this._repository.Create<IServiceProvider>();
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IOneXOneConvertor)))
                .Returns(this._oneXOneConvertorMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IUtilsFactory)))
                .Returns(this._utilsFactoryMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(ILoggerFactory)))
                .Returns(this._loggerFactoryMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IGeoUtils)))
                .Returns(this._geoUtilsMock.Object);
            
            this._s3ClientMock = this._repository.Create<IAmazonS3>();
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IAmazonS3)))
                .Returns(this._s3ClientMock.Object);
            
            this._configurationManagerMock = this._repository.Create<IConfigurationManager>();
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IConfigurationManager)))
                .Returns(this._configurationManagerMock.Object);
            this._configurationManagerMock.Setup(cm => cm.GetConfiguration("S3", "bucket"))
                .Returns("bucket");
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
            var seq = new MockSequence();
            this.SetupConstructorRequiredMocks(true, seq);

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
                this._s3UtilsMock
                    .InSequence(seq)
                    .Setup(utils => utils.TileExists(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => z == 2);
            }

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var s3Source = new S3(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, false); 
            this.VerifyConstructorRequiredMocks();

            var expected = cords.Z == 2;
            if (useCoords)
            {
                Assert.AreEqual(expected, s3Source.TileExists(cords));
            }
            else
            {
                var tile = new Tile(cords, new byte[] { });
                Assert.AreEqual(expected, s3Source.TileExists(tile));
            }

            this._s3UtilsMock.Verify(util => util.TileExists(cords.Z, cords.X, cords.Y),
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
        [DataRow(10, 1, 2, 3, true)]
        public void GetCorrespondingTileWithoutUpscaleWithoutConversion(int batchSize, int z, int x, int y,
            bool expectedNull)
        {
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            this.SetupConstructorRequiredMocks();

            this._s3UtilsMock.Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);

            var s3Source = new S3(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", batchSize, Grid.TwoXOne, GridOrigin.LOWER_LEFT, false);

            var cords = new Coord(z, x, y);
            Assert.AreEqual(expectedNull ? null : existingTile, s3Source.GetCorrespondingTile(cords, false));
            this._s3UtilsMock.Verify(util => util.GetTile(z, x, y), Times.Once);
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
        public void GetCorrespondingTileWithoutUpscale(Coord cords, bool enableUpscale, bool isOneXOne, GridOrigin origin)
        {
            bool expectedNull = cords.Z != 2;
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            var sequence = new MockSequence();
            this.SetupConstructorRequiredMocks(true, sequence);

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
                    this._s3UtilsMock
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
                this._s3UtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);
            }

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var s3Source = new S3(this._pathUtilsMock.Object, this._serviceProviderMock.Object, 
                "test", 10, grid, origin, false);

            var res = s3Source.GetCorrespondingTile(cords, enableUpscale);
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
                    this._s3UtilsMock.Verify(util => util.GetTile(It.Is<Coord>(C => C.Z == cords.Z && C.X == cords.X && C.Y == cords.Y)), Times.Once);
                }
                if (cords.Z == 2)
                {
                    this._oneXOneConvertorMock.Verify(converter => converter.ToTwoXOne(existingTile), Times.Once);
                }
            }
            else
            {
                this._s3UtilsMock.Verify(utils => utils.GetTile(cords.Z, cords.X, cords.Y));
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
            Tile nullTile = null;
            var tile = new Tile(2, 2, 3, new byte[] { });
            var sequence = new MockSequence();
            this.SetupConstructorRequiredMocks(true, sequence);

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
                    this._s3UtilsMock
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
                this._s3UtilsMock
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
                this._s3UtilsMock
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

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var fsSource = new S3(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, false);

            var upscaleCords = new Coord(5, 2, 3);

            var expectedTile = isValidConversion ? tile : null;
            var expectedCallsAfterConversion = isValidConversion ? Times.Once() : Times.Never();
            Assert.AreEqual(expectedTile, fsSource.GetCorrespondingTile(upscaleCords, true));
            if (origin != GridOrigin.LOWER_LEFT)
            {
                this._geoUtilsMock.Verify(utils => utils.FlipY(5, 3), Times.Once);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock.Verify(converter =>
                    converter.TryFromTwoXOne(5, 2, 3), Times.Once);
                this._s3UtilsMock.Verify(utils => utils.GetTile(It.IsAny<Coord>()), expectedCallsAfterConversion);
                this._oneXOneConvertorMock.Verify(converter => converter.TryFromTwoXOne(It.IsAny<Coord>()), Times.Once);
            }
            else
            {
                this._s3UtilsMock.Verify(utils => utils.GetTile(5, 2, 3), Times.Once);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock.Verify(converter => converter.ToTwoXOne(tile), expectedCallsAfterConversion);
            }

            this._s3UtilsMock.Verify(utils => utils.GetTile(4, 1, 1), expectedCallsAfterConversion);
            this._s3UtilsMock.Verify(utils => utils.GetTile(3, 0, 0), expectedCallsAfterConversion);
            this._s3UtilsMock.Verify(utils => utils.GetTile(2, 0, 0), expectedCallsAfterConversion);
            this._s3UtilsMock.Verify(utils => utils.GetTile(1, 0, 0), expectedCallsAfterConversion);
            this._s3UtilsMock.Verify(utils => utils.GetTile(0, 0, 0), expectedCallsAfterConversion);

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
            var testTiles = new Tile[]
            {
                new Tile(1, 2, 3, new byte[] { }), new Tile(7, 7, 7, new byte[] { }),
                new Tile(2, 2, 3, new byte[] { })
            };
            var seq = new MockSequence();
            this.SetupConstructorRequiredMocks(true, seq);

            foreach (var tile in testTiles)
            {
                if (origin == GridOrigin.UPPER_LEFT)
                {
                    this._geoUtilsMock
                        .InSequence(seq)
                        .Setup(utils => utils.FlipY(It.IsAny<Tile>()))
                        .Returns<Tile>(t => t.Y);
                }

                if (isOneXOne)
                {
                    this._oneXOneConvertorMock
                        .InSequence(seq)
                        .Setup(converter => converter.TryFromTwoXOne(It.IsAny<Tile>()))
                        .Returns<Tile>(tile => tile.Z != 7 ? tile : null);
                }

                if (!isOneXOne || tile.Z != 7)
                {
                    this._s3UtilsMock
                        .InSequence(seq)
                        .Setup(utils => utils.UpdateTile(It.IsAny<Tile>()));
                }
            }

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var s3Source = new S3(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, false);

            s3Source.UpdateTiles(testTiles);

            var expectedTiles = isOneXOne ? new Tile[] { testTiles[0], testTiles[2] } : testTiles;
            Func<Tile?, Tile?, bool> tileEqualFunc = (tile1, tile2) => tile1?.Z == tile2?.Z && tile1?.X == tile2?.X && tile1?.Y == tile2?.Y;
            var tileComparer = EqualityComparerFactory.Create<Tile>(tileEqualFunc);
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

            foreach (var tile in expectedTiles)
            {
                this._s3UtilsMock.Verify(utils =>
                utils.UpdateTile(It.Is<Tile>(t => tileEqualFunc(t, tile))), Times.Once);
            }
            this.VerifyAll();
        }

        #endregion

        #region Exists

        public static IEnumerable<object[]> GenExistParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { true, false } //file exists
            );
        }

        [TestMethod]
        [TestCategory("Exists")]
        [DynamicData(nameof(GenExistParams), DynamicDataSourceType.Method)]
        public void Exists(bool isOneXOne, GridOrigin origin, bool exist)
        {
            int listInvocationCount = 1;
            ListObjectsV2Response res = new ListObjectsV2Response() { KeyCount = exist ? 1 : 0 };
            this._s3ClientMock
                .Setup(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var action = () => new S3(this._pathUtilsMock.Object, this._serviceProviderMock.Object, 
                "test", 10, grid, origin, false);

            if (!exist)
            {
                Assert.ThrowsException<Exception>(action);
            }
            else
            {
                var s3Source = action();
                Assert.AreEqual(exist, s3Source.Exists());
                listInvocationCount++;
            }
            
            this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(request =>
                request.BucketName == "bucket" &&
                request.Prefix == "test/" &&
                request.MaxKeys == 1 &&
                request.StartAfter == "test/"), It.IsAny<CancellationToken>()), Times.Exactly(listInvocationCount));
            this.VerifyAll();
        }
        
        [TestMethod]
        [TestCategory("Exists")]
        [DynamicData(nameof(GenExistParams), DynamicDataSourceType.Method)]
        public void S3CreationDefaultExtent(bool isOneXOne, GridOrigin origin, bool exists)
        {
            Extent extent = isOneXOne ?
                new Extent() { MinX = -180, MinY = -180, MaxX = 180, MaxY = 180 }
                :
                new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            ListObjectsV2Response res = new ListObjectsV2Response() { KeyCount = 1 };
            this._s3ClientMock
                .Setup(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);
            this._geoUtilsMock.Setup(geoUtils => geoUtils.DefaultExtent(It.IsAny<bool>())).Returns(extent);

            var s3 = new S3(this._pathUtilsMock.Object, this._serviceProviderMock.Object, 
                "test", 10, grid, origin, false);
            Assert.AreEqual(s3.Extent, extent);
            this.VerifyAll();
        }

        #endregion

        #region TileCount

        public static IEnumerable<object[]> GenTileCountParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { 0, 7, 1365, 782542 } //tile count
            );
        }

        [TestMethod]
        [TestCategory("TileCount")]
        [DynamicData(nameof(GenTileCountParams), DynamicDataSourceType.Method)]
        public void TileCount(bool isOneXOne, GridOrigin origin, int tileCount)
        {
            var seq = new MockSequence();
            int token = 0;
            int tiles = tileCount;
            this.SetupConstructorRequiredMocks(true, seq);

            while (tiles > 1000)
            {
                this._s3ClientMock
                    .InSequence(seq)
                    .Setup(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ListObjectsV2Response() { KeyCount = 1000, NextContinuationToken = token.ToString() });
                token++;
                tiles -= 1000;
            }
            this._s3ClientMock
                .InSequence(seq)
                .Setup(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ListObjectsV2Response() { KeyCount = tiles });

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var s3Source = new S3(this._pathUtilsMock.Object, this._serviceProviderMock.Object, 
                "test", 10, grid, origin, false);

            Assert.AreEqual(tileCount, s3Source.TileCount());

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
            var seq = new MockSequence();
            this.SetupConstructorRequiredMocks(true, seq);

            for (int i = 0; i < Data<IS3Client>.MaxZoomRead; i++)
            {
                this._s3ClientMock
                    .InSequence(seq)
                    .Setup(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ListObjectsV2Response()
                    {
                        S3Objects = new List<S3Object>(0),
                        IsTruncated = false
                    });
            }

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var s3Source = new S3(this._pathUtilsMock.Object, this._serviceProviderMock.Object, 
                "test", 10, grid, origin, false);

            string testIdentifier = offset.ToString();
            s3Source.setBatchIdentifier(testIdentifier);
            s3Source.GetNextBatch(out string batchIdentifier, out string? _, null);
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
            this._s3ClientMock
                .Setup(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ListObjectsV2Response()
                {
                    S3Objects = new List<S3Object>()
                    {
                        new S3Object(),
                        new S3Object(),
                        new S3Object()
                    },
                    NextContinuationToken = "token",
                    KeyCount = 3
                });
            this._s3UtilsMock
                .Setup(utils => utils.GetTile(It.IsAny<string>()))
                .Returns(new Tile(0, 0, 0, Array.Empty<byte>()));
            if (origin != GridOrigin.LOWER_LEFT)
            {
                this._geoUtilsMock
                    .Setup(utils => utils.FlipY(It.IsAny<Tile>()))
                    .Returns<Tile>(t => t.Y);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock
                    .Setup(converter => converter.TryToTwoXOne(It.IsAny<Tile>()))
                    .Returns<Tile>(t => t);
            }
        
            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var s3Source = new S3(this._pathUtilsMock.Object, this._serviceProviderMock.Object, 
                "test", batchSize, grid, origin, false);
        
            s3Source.GetNextBatch(out string batchIdentifier, out string? _, null);
            s3Source.GetNextBatch(out batchIdentifier,out string? _, null);
            Assert.AreNotEqual(null, batchIdentifier);
            s3Source.Reset();
            s3Source.GetNextBatch(out batchIdentifier, out string? _, null);
            Assert.AreEqual("Null", batchIdentifier);
            this.VerifyAll();
        }
        
        #endregion

        #region GetNextBatch

        public static IEnumerable<object[]> GenGetNextBatchParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT }, //origin
                new object[] { 1, 2, 10 }, //tile count
                new object[] { 1, 3, 10 } // s3 response max tile count
            );
        }

        // TODO: add this test when shimon fixes it
        /*[TestMethod]
        [TestCategory("GetNextBatch")]
        [DynamicData(nameof(GenGetNextBatchParams), DynamicDataSourceType.Method)]
        public void GetNextBatch(bool isOneXOne, GridOrigin origin, int batchSize, int s3ResponseTiles)
        {
            var tiles = new Tile?[]
            {
                new Tile(0, 0, 0, new byte[] { }), new Tile(1, 1, 1, new byte[] { }),
                new Tile(2, 2, 2, new byte[] { }), new Tile(3, 3, 3, new byte[] { }),
            };
            var tileBatches = tiles.Chunk(batchSize).ToList();
            var tileZoomCounts = tiles.GroupBy(n => n.Z).Select(g => (g.Key, g.Count())).OrderBy(z => z.Key);
            List<int> requestSizes = new List<int>();
            for (int reqSize = batchSize;
                 reqSize > 0 && batchSize - reqSize < tiles.Length;
                 reqSize -= s3ResponseTiles)
            {
                requestSizes.Add(reqSize);
            }
            var tileIdx = 0;
            int batchId = 0;
            
            var seq = new MockSequence();
            this.SetupConstructorRequiredMocks(seq);

            int countRequests = 0;
            int remainder = 0;
            int currentTile = 0;
            foreach (var group in tileZoomCounts)
            {
                int zoomCount = group.Item2;
                while (zoomCount + remainder >= s3ResponseTiles)
                {
                    remainder = zoomCount + remainder - s3ResponseTiles;
                    zoomCount = 0;
                    // zoomCount -= s3ResponseTiles;
                    // remainder = 0;
                    countRequests++;
                    
                    this._s3ClientMock
                    .InSequence(seq)
                    .Setup(s3 =>
                        s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                    .Returns<ListObjectsV2Request, CancellationToken>((req, _) =>
                    {
                        int remaining = tiles.Length - tileIdx;
                        int keys = new int[] { req.MaxKeys, s3ResponseTiles, remaining }.Min();
                        bool done = keys == remaining;
                        var res = new ListObjectsV2Response()
                        {
                            KeyCount = keys,
                            NextContinuationToken = done ? null : batchId.ToString(),
                            IsTruncated = !done,
                            S3Objects = tiles.Skip(tileIdx).Take(keys)
                                .Select(t => new S3Object() { Key = $"{t.Z}/{t.X}/{t.Y}.png" }).ToList()
                        };
                        tileIdx += keys;
                        batchId++;
                        return Task.FromResult(res);
                    });

                    for (int j = 0; j < s3ResponseTiles; j++)
                    {
                        currentTile++;
                        
                        this._s3UtilsMock
                            .InSequence(seq)
                            .Setup(utils => utils.GetTile(It.IsAny<string>()))
                            .Returns<string>(key => tiles[int.Parse(key[..1])]);

                        if (isOneXOne)
                        {
                            this._oneXOneConvertorMock
                                .InSequence(seq)
                                .Setup(converter => converter.TryToTwoXOne(It.IsAny<Tile>()))
                                .Returns<Tile>(tile => tile.Z != 0 ? tile : null);
                        }

                        if (origin == GridOrigin.UPPER_LEFT && (!isOneXOne || tiles[currentTile]?.Z != 0))
                        {
                            this._geoUtilsMock
                                .InSequence(seq)
                                .Setup(converter => converter.FlipY(It.IsAny<Tile>()))
                                .Returns<Tile>(t => t.Y);
                        }
                    }
                }

                remainder += zoomCount;
            }

            if (remainder > 0)
            {
                countRequests++;
            }

            // for (int i = 0; i < countRequests; i++)
            // {
                this._s3ClientMock
                    .InSequence(seq)
                    .Setup(s3 =>
                        s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                    .Returns<ListObjectsV2Request, CancellationToken>((req, _) =>
                    {
                        int remaining = tiles.Length - tileIdx;
                        int keys = new int[] { req.MaxKeys, s3ResponseTiles, remaining }.Min();
                        bool done = keys == remaining;
                        var res = new ListObjectsV2Response()
                        {
                            KeyCount = keys,
                            NextContinuationToken = done ? null : batchId.ToString(),
                            IsTruncated = !done,
                            S3Objects = tiles.Skip(tileIdx).Take(keys)
                                .Select(t => new S3Object() { Key = $"{t.Z}/{t.X}/{t.Y}.png" }).ToList()
                        };
                        tileIdx += keys;
                        batchId++;
                        return Task.FromResult(res);
                    });

                for (int j = 0; j < remainder; j++)
                {
                    currentTile++;
                    this._s3UtilsMock
                        .InSequence(seq)
                        .Setup(utils => utils.GetTile(It.IsAny<string>()))
                        .Returns<string>(key => tiles[int.Parse(key[..1])]);

                    if (isOneXOne)
                    {
                        this._oneXOneConvertorMock
                            .InSequence(seq)
                            .Setup(converter => converter.TryToTwoXOne(It.IsAny<Tile>()))
                            .Returns<Tile>(tile => tile.Z != 0 ? tile : null);
                    }

                    if (origin == GridOrigin.UPPER_LEFT && (!isOneXOne || tiles[currentTile]?.Z != 0))
                    {
                        this._geoUtilsMock
                            .InSequence(seq)
                            .Setup(converter => converter.FlipY(It.IsAny<Tile>()))
                            .Returns<Tile>(t => t.Y);
                    }
                }
            // }

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var s3Source = new S3(this._pathUtilsMock.Object, this._s3ClientMock.Object, this._serviceProviderMock.Object, "bucket", "test", batchSize, grid, origin);
            VerifyConstructorRequiredMocks();
            // for (int z = 0; z < Data<IS3Client>.MaxZoomRead; z++)
            // {
            //     this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(req =>
            //         req.BucketName == "bucket" &&
            //         req.Prefix == $"test/{z}/" &&
            //         req.StartAfter == $"test/{z}/" &&
            //         req.MaxKeys == 1
            //     ), It.IsAny<CancellationToken>()), Times.Once);
            // }
            
            var comparer = ComparerFactory.Create<Tile>((t1, t2) => t1?.Z == t2?.Z && t1?.X == t2?.X && t1?.Y == t2?.Y ? 0 : -1);
            for (int i = 0; i < tileBatches.Count; i++)
            {
                var exactedBatch = tileBatches[i].Where(t => !isOneXOne || t.Z != 0);
                var res = s3Source.GetNextBatch(out string batchIdentifier);
                CollectionAssert.AreEqual(exactedBatch.ToArray(), res, comparer);
                int batch = (i * (int)Math.Ceiling((double)batchSize / s3ResponseTiles)) - 1;
                string? expectedBatchId = batch < 0 ? null : batch.ToString();
                Assert.AreEqual(expectedBatchId, batchIdentifier);

                int s3BatchNum = batch;
                var tileZooms = res.Select(tile => tile.Z).OrderBy(z => z);

                // Check amount of calls to ListObjectsV2Async
                // if (s3ResponseTiles != 1)
                // {
                // foreach (var z in tileZooms)
                // {
                //     this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(req =>
                //         req.BucketName == "bucket" &&
                //         req.ContinuationToken == (s3BatchNum < 0 ? null : s3BatchNum.ToString()) &&
                //         req.Prefix == $"test/{z}/" &&
                //         req.StartAfter == $"test/{z}/" &&
                //         req.MaxKeys == It.IsAny<int>()
                //     ), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
                // }
                // }
                // else
                // {
                //     for (int z = 0; z < Data<IS3Client>.MaxZoomRead; z++)
                //     {
                //         this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(req =>
                //             req.BucketName == "bucket" &&
                //             req.ContinuationToken == (s3BatchNum < 0 ? null : s3BatchNum.ToString()) &&
                //             req.Prefix == $"test/{z}/" &&
                //             req.StartAfter == $"test/{z}/" &&
                //             req.MaxKeys == 1
                //         ), It.IsAny<CancellationToken>()), tileZooms.Contains(z) ? Times.Exactly(2) : Times.Exactly(1));
                //     }
                // }

                // this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(req =>
                //         req.BucketName == "bucket" &&
                //         req.ContinuationToken == (s3BatchNum < 0 ? null : s3BatchNum.ToString()) &&
                //         req.Prefix == "test/" &&
                //         req.StartAfter == "test/" &&
                //         req.MaxKeys == 1
                //     ), It.IsAny<CancellationToken>()), Times.Exactly(Data<IS3Client>.MaxZoomRead));
                
                // tileZoomCounts = res.GroupBy(n => n.Z).Select(g => (g.Key, g.Count())).OrderBy(z => z.Key);
                //
                // foreach (var item in tileZoomCounts.Zip(requestSizes))
                // {
                //     int z = item.First.Key;
                //     int count = item.First.Item2 + 1;
                //     int requestSize = item.Second;
                //     // var times = tileZoomCounts.Contains(item.First) ? Times.Exactly(2) : Times.Exactly(1);
                //     this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(req =>
                //         req.BucketName == "bucket" &&
                //         req.ContinuationToken == (s3BatchNum < 0 ? null : s3BatchNum.ToString()) &&
                //         req.Prefix == $"test/{z}/" &&
                //         req.StartAfter == $"test/{z}/" &&
                //         req.MaxKeys == requestSize
                //     ), It.IsAny<CancellationToken>()), Times.AtLeast(1));
                // }
                
                // for (int reqSize = batchSize; reqSize > 0 && batchSize - reqSize < tiles.Length; reqSize -= s3ResponseTiles)
                // {
                //     this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(req =>
                //         req.BucketName == "bucket" &&
                //         req.ContinuationToken == (s3BatchNum < 0 ? null : s3BatchNum.ToString()) &&
                //         req.Prefix == "test/" &&
                //         req.StartAfter == "test/" &&
                //         req.MaxKeys == reqSize
                //     ), It.IsAny<CancellationToken>()), Times.Once);
                //     s3BatchNum++;
                // }

                foreach (var tile in tileBatches[i])
                {
                    if (!isOneXOne || tile.Z != 0)
                    {
                        this._s3UtilsMock.Verify(utils =>
                            utils.GetTile($"{tile.Z}/{tile.X}/{tile.Y}.png"));
                    }

                    if (origin == GridOrigin.UPPER_LEFT && (!isOneXOne || tile.Z != 0))
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

            // int calls = (int)Math.Ceiling((double)Math.Min(batchSize, tiles.Length) / s3ResponseTiles) * (int)Math.Ceiling((double)tiles.Length / batchSize);
            this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()), Times.Exactly(countRequests + Data<IS3Client>.MaxZoomRead));
            this._s3UtilsMock.Verify(utils => utils.GetTile(It.IsAny<string>()), Times.Exactly(tiles.Length));
            this.VerifyAll();
        }*/

        #endregion

        #region helper

        private void SetupConstructorRequiredMocks(bool exists = true, MockSequence? sequence = null)
        {
            var seq = sequence ?? new MockSequence();
            
            ListObjectsV2Response res = new ListObjectsV2Response() { KeyCount = exists ? 1 : 0 };
            this._s3ClientMock
                .InSequence(seq)
                .Setup(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            // Mock existance check for each zoom level until MaxZoomRead
            for (int i = 0; i < Data<IS3Client>.MaxZoomRead; i++)
            {
                this._s3ClientMock
                    .InSequence(seq)
                    .Setup(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ListObjectsV2Response() { KeyCount = 1 });
            }
        }

        private void VerifyConstructorRequiredMocks()
        {
            for (int z = 0; z < Data<IS3Client>.MaxZoomRead; z++)
            {
                this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(req =>
                    req.BucketName == "bucket" &&
                    req.Prefix == $"test/{z}/" &&
                    req.StartAfter == $"test/{z}/" &&
                    req.MaxKeys == 1
                ), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        private void VerifyAll()
        {
            this._s3UtilsMock.VerifyAll();
            this._pathUtilsMock.VerifyAll();
            this._oneXOneConvertorMock.VerifyAll();
            this._geoUtilsMock.VerifyAll();
            this._s3ClientMock.VerifyAll();
        }

        #endregion
    }
}
