using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace MergerLogicUnitTests.DataTypes
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("FS")]
    [TestCategory("FSDataSource")]
    public class FSTest
    {
        #region mocks

        private MockRepository _repository;
        private Mock<IServiceProvider> _serviceProviderMock;
        private Mock<IOneXOneConvertor> _oneXOneConvertorMock;
        private Mock<IUtilsFactory> _utilsFactoryMock;
        private Mock<IFileClient> _fsUtilsMock;
        private Mock<IGeoUtils> _geoUtilsMock;
        private Mock<IPathUtils> _pathUtilsMock;
        private Mock<ILoggerFactory> _loggerFactoryMock;
        private Mock<ILogger<FS>> _loggerMock;
        private Mock<IFileSystem> _fileSystemMock;
        private Mock<IDirectory> _directoryMock;
        private Mock<IFileInfoFactory> _fileInfoFactoryMock;
        private Mock<IPath> _pathMock;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._oneXOneConvertorMock = this._repository.Create<IOneXOneConvertor>();
            this._fsUtilsMock = this._repository.Create<IFileClient>();
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();
            this._pathUtilsMock = this._repository.Create<IPathUtils>();
            this._directoryMock = this._repository.Create<IDirectory>();
            this._fileInfoFactoryMock = this._repository.Create<IFileInfoFactory>();
            this._pathMock = this._repository.Create<IPath>();
            this._fileSystemMock = this._repository.Create<IFileSystem>();
            this._fileSystemMock.SetupGet(fs => fs.Directory).Returns(this._directoryMock.Object);
            this._fileSystemMock.SetupGet(fs => fs.FileInfo).Returns(this._fileInfoFactoryMock.Object);
            this._fileSystemMock.SetupGet(fs => fs.Path).Returns(this._pathMock.Object);
            this._utilsFactoryMock = this._repository.Create<IUtilsFactory>();
            this._utilsFactoryMock.Setup(factory => factory.GetDataUtils<IFileClient>(It.IsAny<string>()))
                .Returns(this._fsUtilsMock.Object);
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
            this._serviceProviderMock.Setup(container => container.GetService(typeof(IFileSystem)))
                .Returns(this._fileSystemMock.Object);
            this._serviceProviderMock.Setup(container => container.GetService(typeof(ILoggerFactory)))
                .Returns(this._loggerFactoryMock.Object);
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
            this.SetupConstructorRequiredMocks(isBase);
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
                this._fsUtilsMock
                    .InSequence(seq)
                    .Setup(utils => utils.TileExists(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => z == 2);
            }

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, isBase);

            var expected = cords.Z == 2;
            if (useCoords)
            {
                Assert.AreEqual(expected, fsSource.TileExists(cords));
            }
            else
            {
                var tile = new Tile(cords, new byte[] { });
                Assert.AreEqual(expected, fsSource.TileExists(tile));
            }
            this._fsUtilsMock.Verify(util => util.TileExists(cords.Z, cords.X, cords.Y),
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
            this.SetupConstructorRequiredMocks(isBase);
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            this._fsUtilsMock.Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);

            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", batchSize, Grid.TwoXOne, GridOrigin.LOWER_LEFT, isBase);

            var cords = new Coord(z, x, y);
            Assert.AreEqual(expectedNull ? null : existingTile, fsSource.GetCorrespondingTile(cords, false));
            this._fsUtilsMock.Verify(util => util.GetTile(z, x, y), Times.Once);
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
            this.SetupConstructorRequiredMocks(isBase);
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
                    this._fsUtilsMock
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
                this._fsUtilsMock
                    .InSequence(sequence)
                    .Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                    .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);
            }

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, isBase);

            var res = fsSource.GetCorrespondingTile(cords, enableUpscale);
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
                    this._fsUtilsMock.Verify(util => util.GetTile(It.Is<Coord>(C => C.Z == cords.Z && C.X == cords.X && C.Y == cords.Y)), Times.Once);
                }
                if (cords.Z == 2)
                {
                    this._oneXOneConvertorMock.Verify(converter => converter.ToTwoXOne(existingTile), Times.Once);
                }
            }
            else
            {
                this._fsUtilsMock.Verify(utils => utils.GetTile(cords.Z, cords.X, cords.Y));
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
            this.SetupConstructorRequiredMocks(isBase);
            Tile nullTile = null;
            var tile = new Tile(2, 2, 3, new byte[] { });
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
                    this._fsUtilsMock
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
                this._fsUtilsMock
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
                this._fsUtilsMock
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
            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, isBase);
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
                this._fsUtilsMock.Verify(utils => utils.GetTile(It.IsAny<Coord>()), expectedCallsAfterConversion);
                this._oneXOneConvertorMock.Verify(converter => converter.TryFromTwoXOne(It.IsAny<Coord>()), Times.Once);
            }
            else
            {
                this._fsUtilsMock.Verify(utils => utils.GetTile(5, 2, 3), Times.Once);
            }
            if (isOneXOne)
            {
                this._oneXOneConvertorMock.Verify(converter => converter.ToTwoXOne(tile), expectedCallsAfterConversion);
            }


            this._fsUtilsMock.Verify(utils => utils.GetTile(4, 1, 1), expectedCallsAfterConversion);
            this._fsUtilsMock.Verify(utils => utils.GetTile(3, 0, 0), expectedCallsAfterConversion);
            this._fsUtilsMock.Verify(utils => utils.GetTile(2, 0, 0), expectedCallsAfterConversion);
            this._fsUtilsMock.Verify(utils => utils.GetTile(1, 0, 0), expectedCallsAfterConversion);
            this._fsUtilsMock.Verify(utils => utils.GetTile(0, 0, 0), expectedCallsAfterConversion);

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
            this.SetupConstructorRequiredMocks(isBase);

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
            var directoryMock = new Mock<IDirectoryInfo>(MockBehavior.Strict);
            directoryMock.Setup(di => di.Create());
            var fileMock = new Mock<IFileInfo>(MockBehavior.Strict);
            fileMock.SetupGet(fi => fi.Directory).Returns(directoryMock.Object);
            this._fileInfoFactoryMock
                .Setup(fac => fac.FromFileName(It.IsAny<string>()))
                .Returns(fileMock.Object);
            this._pathUtilsMock.Setup(utils => utils.GetTilePath("test", It.IsAny<Tile>())).Returns("testPath");

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, isBase);

            var testTiles = new Tile[]
            {
                    new Tile(1, 2, 3, new byte[] { 0}), new Tile(7, 7, 7, new byte[] { 1}),
                    new Tile(2, 2, 3, new byte[] { 2})
            };

            var tileComparer = EqualityComparerFactory.Create<Tile>((tile1, tile2) =>
                tile1?.Z == tile2?.Z &&
                tile1?.X == tile2?.X &&
                tile1?.Y == tile2?.Y);

            //since asserts end the function this is the simplest way to make sure dispose is always called
            using (var fileStream1 = new MemoryStream())
            using (var fileStream2 = new MemoryStream())
            using (var fileStream3 = new MemoryStream())
            {
                var fileStreams = new MemoryStream[] { fileStream1, fileStream2, fileStream3 };

                var seq = new MockSequence();
                foreach (var fileStream in fileStreams)
                {
                    fileMock.InSequence(seq).Setup(f => f.OpenWrite()).Returns(fileStream);
                }

                fsSource.UpdateTiles(testTiles);

                CollectionAssert.AreEqual(new byte[] { 0 }, fileStreams[0].ToArray());
                if (isOneXOne)
                {
                    CollectionAssert.AreEqual(new byte[] { 2 }, fileStreams[1].ToArray());
                }
                else
                {
                    CollectionAssert.AreEqual(new byte[] { 1 }, fileStreams[1].ToArray());
                    CollectionAssert.AreEqual(new byte[] { 2 }, fileStreams[2].ToArray());
                }
            }


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

            int expectedCreateCalls = isOneXOne ? 2 : 3;
            directoryMock.Verify(di => di.Create(), Times.Exactly(expectedCreateCalls));
            this.VerifyAll();
        }

        #endregion

        #region Wrapup

        public static IEnumerable<object[]> GenWrapupParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { true, false }, //is base
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT } //origin
            );
        }

        [TestMethod]
        [TestCategory("Wrapup")]
        [DynamicData(nameof(GenWrapupParams), DynamicDataSourceType.Method)]
        public void Wrapup(bool isOneXOne, bool isBase, GridOrigin origin)
        {
            this.SetupConstructorRequiredMocks(isBase);
            this._fileSystemMock
                .Setup(fs => fs.Directory.EnumerateFiles(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
                .Returns(Array.Empty<string>());

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, isBase);

            fsSource.Wrapup();

            this._fileSystemMock
               .Verify(fs => fs.Directory.EnumerateFiles(It.IsAny<string>(), "*.*", SearchOption.AllDirectories), Times.Exactly(2));

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
            this.SetupConstructorRequiredMocks(isBase);
            var seq = new MockSequence();
            this._pathMock
                .InSequence(seq)
                .Setup(path => path.GetFullPath("test"))
                .Returns("/test/test");
            this._directoryMock
                .InSequence(seq)
                .Setup(directory => directory.Exists("/test/test"))
                .Returns(exist);

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, isBase);

            Assert.AreEqual(exist, fsSource.Exists());

            this._pathMock.Verify(path => path.GetFullPath("test"), Times.Once);
            this._directoryMock.Verify(directory => directory.Exists("/test/test"), Times.Once);

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
            var seq = new MockSequence();
            this.SetupConstructorRequiredMocks(isBase, seq);
            var fileList = new List<string>();
            for (int i = 0; i < tileCount; i++)
            {
                //valid files
                fileList.Add(i % 2 == 0 ? "t.png" : "t.jpg");
                //invalid files
                fileList.Add(string.Empty);
            }

            this._directoryMock
                .InSequence(seq)
                .Setup(d => d.EnumerateFiles("test", "*.*", SearchOption.AllDirectories))
                .Returns(fileList);

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, isBase);

            Assert.AreEqual(tileCount, fsSource.TileCount());
            this._directoryMock.Verify(d => d.EnumerateFiles("test", "*.*", SearchOption.AllDirectories), Times.Exactly(2));
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
                new object[] { 3, 5 } //batch offset
            );
        }

        [TestMethod]
        [TestCategory("SetBatchIdentifier")]
        [DynamicData(nameof(GenSetBatchIdentifierParams), DynamicDataSourceType.Method)]
        public void SetBatchIdentifier(bool isOneXOne, bool isBase, GridOrigin origin, int offset)
        {
            this.SetupConstructorRequiredMocks(isBase);

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, isBase);

            string testIdentifier = offset.ToString();
            fsSource.setBatchIdentifier(testIdentifier);
            fsSource.GetNextBatch(out string batchIdentifier);
            Assert.AreEqual(testIdentifier, batchIdentifier);

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
                new object[] { 1, 2 } //batch size
            );
        }

        [TestMethod]
        [TestCategory("Reset")]
        [DynamicData(nameof(GenResetParams), DynamicDataSourceType.Method)]
        public void Reset(bool isOneXOne, bool isBase, GridOrigin origin, int batchSize)
        {
            this.SetupConstructorRequiredMocks(isBase);
            var fileList = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                //valid files
                fileList.Add(i % 2 == 0 ? "t.png" : "t.jpg");
                //invalid files
                fileList.Add(string.Empty);
            }
            this._directoryMock
                .Setup(d => d.EnumerateFiles("test", "*.*", SearchOption.AllDirectories))
                .Returns(fileList);
            this._pathUtilsMock
                .Setup(utils => utils.FromPath(It.IsAny<string>(), false))
                .Returns(new Coord(0, 0, 0));
            this._fsUtilsMock
                .Setup(utils => utils.GetTile(It.IsAny<Coord>()))
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
            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", batchSize, grid, origin, isBase);

            fsSource.GetNextBatch(out string batchIdentifier);
            fsSource.GetNextBatch(out batchIdentifier);
            Assert.AreNotEqual("0", batchIdentifier);
            fsSource.Reset();
            fsSource.GetNextBatch(out batchIdentifier);
            Assert.AreEqual("0", batchIdentifier);
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
            var tiles = new Tile?[]
            {
                new Tile(0, 0, 0, new byte[] { }), new Tile(1, 1, 1, new byte[] { }), null,
                new Tile(3, 3, 3, new byte[] { }), new Tile(4, 4, 4, new byte[] { }),
            };
            var tileBatches = tiles.Where(t => t is not null && (!isOneXOne || t.Z != 0)).Chunk(batchSize).ToList();
            var batchIdx = 0;
            this.SetupConstructorRequiredMocks(isBase);
            var seq = new MockSequence();
            this._fileSystemMock
                .InSequence(seq)
                .Setup(fs => fs.Directory.EnumerateFiles(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
                .Returns(new string[] { "0.png", "1.jpg", "2.png", "invalid", "3.png", "4.png" });

            foreach (var tile in tiles)
            {
                this._pathUtilsMock
                    .InSequence(seq)
                    .Setup(utils => utils.FromPath(It.IsAny<string>(), false))
                    .Returns<string, bool>((path, _) =>
                    {
                        int coord = int.Parse(path[..1]);
                        return new Coord(coord, coord, coord);
                    });
                this._fsUtilsMock
                    .InSequence(seq)
                    .Setup(utils => utils.GetTile(It.IsAny<Coord>()))
                    .Returns<Coord>(c => tiles[c.Z]);
                if (tile != null)
                {
                    if (isOneXOne)
                    {
                        this._oneXOneConvertorMock
                            .InSequence(seq)
                            .Setup(converter => converter.TryToTwoXOne(It.IsAny<Tile>()))
                            .Returns<Tile>(tile => tile.Z != 0 ? tile : null);
                    }

                    if (origin == GridOrigin.UPPER_LEFT && (!isOneXOne || tile.Z != 0))
                    {
                        this._geoUtilsMock
                            .InSequence(seq)
                            .Setup(converter => converter.FlipY(It.IsAny<Tile>()))
                            .Returns<Tile>(t => t.Y);
                    }
                }
            }

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            var fsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", batchSize, grid, origin, isBase);

            var comparer = ComparerFactory.Create<Tile>((t1, t2) => t1?.Z == t2?.Z && t1?.X == t2?.X && t1?.Y == t2?.Y ? 0 : -1);
            for (int i = 0; i < tileBatches.Count; i++)
            {
                var exactedBatch = tileBatches[i];
                var res = fsSource.GetNextBatch(out string batchIdentifier);

                CollectionAssert.AreEqual(exactedBatch.ToArray(), res, comparer);
                string expectedBatchId = Math.Min(i * batchSize, tiles.Length).ToString();
                Assert.AreEqual(expectedBatchId, batchIdentifier);
                foreach (var tile in tileBatches[i])
                {
                    this._pathUtilsMock.Verify(utils => utils.FromPath(It.IsRegex($"^{tile.Z}\\.(png|jpg)$"), false), Times.Once);
                    this._fsUtilsMock.Verify(utils => utils.GetTile(It.Is<Coord>(c => c.Z == tile.Z && c.X == tile.X && c.Y == tile.Y)));
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

        #region FsCreation

        public static IEnumerable<object[]> GenFsCreationParams()
        {
            return DynamicDataGenerator.GeneratePrams(
                new object[] { true, false }, //is one on one
                new object[] { true, false }, //is base
                new object[] { GridOrigin.LOWER_LEFT, GridOrigin.UPPER_LEFT } //origin
            );
        }

        [TestMethod]
        [TestCategory("FsCreation")]
        [DynamicData(nameof(GenFsCreationParams), DynamicDataSourceType.Method)]
        public void FsCreation(bool isOneXOne, bool isBase, GridOrigin origin)
        {
            var seq = new MockSequence();

            IDirectoryInfo nullInfo = null;
            if (isBase)
            {
                this._directoryMock
                    .InSequence(seq)
                    .Setup(directory => directory.CreateDirectory(It.IsAny<string>())).Returns(nullInfo);
            }

            this._fileSystemMock
                .InSequence(seq)
                .Setup(fs => fs.Directory.EnumerateFiles(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
                .Returns(Array.Empty<string>());

            Grid grid = isOneXOne ? Grid.OneXOne : Grid.TwoXOne;
            new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, grid, origin, isBase);

            this._directoryMock.Verify(dir => dir.CreateDirectory("test"), isBase ? Times.Once : Times.Never);
            this.VerifyAll();
        }



        #endregion

        #region helper

        private void SetupConstructorRequiredMocks(bool isBase, MockSequence? sequence = null)
        {
            var seq = sequence ?? new MockSequence();
            if (isBase)
            {
                IDirectoryInfo nullInfo = null;
                this._directoryMock
                    .InSequence(seq)
                    .Setup(directory => directory.CreateDirectory(It.IsAny<string>())).Returns(nullInfo);
            }

            this._fileSystemMock
                .InSequence(seq)
                .Setup(fs => fs.Directory.EnumerateFiles(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
                .Returns(Array.Empty<string>());

        }

        private void VerifyAll()
        {
            this._fsUtilsMock.VerifyAll();
            this._directoryMock.VerifyAll();
            this._fileInfoFactoryMock.VerifyAll();
            this._pathUtilsMock.VerifyAll();
            this._oneXOneConvertorMock.VerifyAll();
            this._geoUtilsMock.VerifyAll();
            this._pathMock.VerifyAll();
        }

        #endregion
    }
}
