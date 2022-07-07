﻿using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using MergerLogicUnitTests.utils;
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
    [TestCategory("WMTS")]
    [TestCategory("WMTSDataSource")]
    [TestCategory("HttpDataSource")]

    public class WMTSTest
    {
        #region mocks

        private MockRepository _repository;
        private Mock<IServiceProvider> _serviceProviderMock;
        private Mock<IOneXOneConvertor> _oneXOneConvertorMock;
        private Mock<IUtilsFactory> _utilsFactoryMock;
        private Mock<IHttpUtils> _httpUtilsMock;
        private Mock<IGeoUtils> _geoUtilsMock;
        private Mock<ILoggerFactory> _loggerFactoryMock;
        private Mock<ILogger<FS>> _loggerMock;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._oneXOneConvertorMock = this._repository.Create<IOneXOneConvertor>();
            this._httpUtilsMock = this._repository.Create<IHttpUtils>();
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();
            this._utilsFactoryMock = this._repository.Create<IUtilsFactory>();
            this._utilsFactoryMock.Setup(factory => factory.GetDataUtils<IHttpUtils>(It.IsAny<string>()))
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
            if (origin == GridOrigin.LOWER_LEFT)
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
            var wmtsSource = new WMTS(this._serviceProviderMock.Object, "test", 10, extent, 21, 0, isOneXOne, origin);

            var expected = cords.Z == 2;
            if (useCoords)
            {
                Assert.AreEqual(expected, wmtsSource.TileExists(cords));
            }
            else
            {
                var tile = new Tile(cords, new byte[] { });
                Assert.AreEqual(expected, wmtsSource.TileExists(tile));
            }
            this._httpUtilsMock.Verify(util => util.TileExists(cords.Z, cords.X, cords.Y),
                cords.Z != 0 || !isOneXOne
                    ? Times.Once
                    : Times.Never);
            this._geoUtilsMock.Verify(utils => utils.FlipY(It.Is<Coord>(c => c.Z == cords.Z && c.X == cords.X && c.Y == cords.Y)),
                    origin == GridOrigin.LOWER_LEFT
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
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            this._httpUtilsMock.Setup(utils => utils.GetTile(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<int, int, int>((z, x, y) => z == 2 ? existingTile : nullTile);

            var extent = new Extent() { MinX = -180, MinY = -90, MaxX = 180, MaxY = 90 };
            var wmtsSource = new WMTS(this._serviceProviderMock.Object, "test", batchSize, extent, 21, 0, false, GridOrigin.UPPER_LEFT);

            var cords = new Coord(z, x, y);
            Assert.AreEqual(expectedNull ? null : existingTile, wmtsSource.GetCorrespondingTile(cords, false));
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
        public void GetCorrespondingTileWithoutUpscale(Coord cords, bool enableUpscale, bool isOneXOne, GridOrigin origin)
        {
            bool expectedNull = cords.Z != 2;
            this.SetupConstructorRequiredMocks();
            Tile nullTile = null;
            var existingTile = new Tile(2, 2, 3, new byte[] { });
            var sequence = new MockSequence();
            if (origin != GridOrigin.UPPER_LEFT)
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
            var wmtsSource = new WMTS(this._serviceProviderMock.Object, "test", 10, extent, 21, 0, isOneXOne, origin);

            var res = wmtsSource.GetCorrespondingTile(cords, enableUpscale);
            if (expectedNull)
            {
                Assert.IsNull(res);
            }
            else
            {
                Assert.IsTrue(res.Z == 2 && res.X == 2 && res.Y == 3);
            }

            if (origin != GridOrigin.UPPER_LEFT)
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
            var tile = new Tile(2, 2, 3, new byte[] { });
            var sequence = new MockSequence();

            if (origin != GridOrigin.UPPER_LEFT)
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
            var wmtsSource = new WMTS(this._serviceProviderMock.Object, "test", 10, extent, 21, 0, isOneXOne, origin);
            var upscaleCords = new Coord(5, 2, 3);

            var expectedTile = isValidConversion ? tile : null;
            var expectedCallsAfterConversion = isValidConversion ? Times.Once() : Times.Never();
            Assert.AreEqual(expectedTile, wmtsSource.GetCorrespondingTile(upscaleCords, true));
            if (origin != GridOrigin.UPPER_LEFT)
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
            var wmtsSource = new WMTS(this._serviceProviderMock.Object, "test", 10, extent, 21, 0, isOneXOne, origin);

            var testTiles = new Tile[]
            {
                    new Tile(1, 2, 3, new byte[] { 0}), new Tile(7, 7, 7, new byte[] { 1}),
                    new Tile(2, 2, 3, new byte[] { 2})
            };

            Assert.ThrowsException<NotImplementedException>(() => wmtsSource.UpdateTiles(testTiles));
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
            var wmtsSource = new WMTS(this._serviceProviderMock.Object, "test", 10, extent, 21, 0, isOneXOne, origin);

            Assert.AreEqual(exist, wmtsSource.Exists());

            this.VerifyAll();
        }

        #endregion
        /*
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

            var wmtsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, isOneXOne, isBase, origin);

            Assert.AreEqual(tileCount, wmtsSource.TileCount());
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

            var wmtsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, isOneXOne, isBase, origin);

            string testIdentifier = offset.ToString();
            wmtsSource.setBatchIdentifier(testIdentifier);
            wmtsSource.GetNextBatch(out string batchIdentifier);
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
            this._httpUtilsMock
                .Setup(utils => utils.GetTile(It.IsAny<Coord>()))
                .Returns(new Tile(0, 0, 0, Array.Empty<byte>()));
            if (origin != GridOrigin.UPPER_LEFT)
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

            var wmtsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", batchSize, isOneXOne, isBase, origin);

            wmtsSource.GetNextBatch(out string batchIdentifier);
            wmtsSource.GetNextBatch(out batchIdentifier);
            Assert.AreNotEqual("0", batchIdentifier);
            wmtsSource.Reset();
            wmtsSource.GetNextBatch(out batchIdentifier);
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
                this._httpUtilsMock
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

                    if (origin == GridOrigin.LOWER_LEFT && (!isOneXOne || tile.Z != 0))
                    {
                        this._geoUtilsMock
                            .InSequence(seq)
                            .Setup(converter => converter.FlipY(It.IsAny<Tile>()))
                            .Returns<Tile>(t => t.Y);
                    }
                }
            }

            var wmtsSource = new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", batchSize, isOneXOne, isBase, origin);

            var comparer = ComparerFactory.Create<Tile>((t1, t2) => t1?.Z == t2?.Z && t1?.X == t2?.X && t1?.Y == t2?.Y ? 0 : -1);
            for (int i = 0; i < tileBatches.Count; i++)
            {
                var exactedBatch = tileBatches[i];
                var res = wmtsSource.GetNextBatch(out string batchIdentifier);

                CollectionAssert.AreEqual(exactedBatch.ToArray(), res, comparer);
                string expectedBatchId = Math.Min(i * batchSize, tiles.Length).ToString();
                Assert.AreEqual(expectedBatchId, batchIdentifier);
                foreach (var tile in tileBatches[i])
                {
                    this._pathUtilsMock.Verify(utils => utils.FromPath(It.IsRegex($"^{tile.Z}\\.(png|jpg)$"), false), Times.Once);
                    this._httpUtilsMock.Verify(utils => utils.GetTile(It.Is<Coord>(c => c.Z == tile.Z && c.X == tile.X && c.Y == tile.Y)));
                    if (origin == GridOrigin.LOWER_LEFT)
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

            new FS(this._pathUtilsMock.Object, this._serviceProviderMock.Object, "test", 10, isOneXOne, isBase, origin);

            this._directoryMock.Verify(dir => dir.CreateDirectory("test"), isBase ? Times.Once : Times.Never);
            this.VerifyAll();
        }



        #endregion
        */
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
