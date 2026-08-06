using MergerLogic.Clients;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;

namespace MergerLogicUnitTests.Clients
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("fs")]
    [TestCategory("FileClient")]
    [DeploymentItem(@"../../../Clients/TestImages")]
    public class FileClientTest
    {
        #region mocks
        private MockRepository _repository;
        private Mock<IGeoUtils> _geoUtilsMock;
        private Mock<IFileSystem> _fsMock;
        private Mock<IFile> _fileMock;
        private Mock<IPath> _pathMock;
        private Mock<IDirectory> _directoryMock;
        private Mock<IImageFormatter> _imageFormatterMock;
        private byte[] _jpegImageData;
        private byte[] _pngImageData;
        #endregion

        [TestInitialize]
        public void beforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();
            this._fsMock = new Mock<IFileSystem>(MockBehavior.Strict);
            this._fileMock = this._repository.Create<IFile>();
            this._pathMock = this._repository.Create<IPath>();
            this._directoryMock = this._repository.Create<IDirectory>();
            this._fsMock.SetupGet(fs => fs.File).Returns(this._fileMock.Object);
            this._fsMock.SetupGet(fs => fs.Path).Returns(this._pathMock.Object);
            this._fsMock.SetupGet(fs => fs.Directory).Returns(this._directoryMock.Object);
            this._imageFormatterMock = this._repository.Create<IImageFormatter>();

            this._jpegImageData = File.ReadAllBytes("image.jpeg");
            this._pngImageData = File.ReadAllBytes("image.png");
        }

        #region GetTile

        public static IEnumerable<object[]> GetGetTileParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
            {
                new object[] { true, false }, //useCoords
                new object[] { true, false }, // return null
                new object[] { TileFormat.Png, TileFormat.Jpeg }
            });
        }

        [TestMethod]
        [DynamicData(nameof(GetGetTileParams), DynamicDataSourceType.Method)]
        public void GetTile(bool useCoords, bool returnsNull, TileFormat targetFormat)
        {
            Coord cords = new Coord(1, 2, 3);
            byte[] data = targetFormat == TileFormat.Jpeg ? this._jpegImageData : this._pngImageData;

            SetupTilePathProbe(cords, returnsNull, targetFormat, out string? foundPath);
            if (!returnsNull)
            {
                this._fileMock
                    .Setup(util => util.ReadAllBytes(foundPath))
                    .Returns(data);
            }

            var fileClient = new FileClient("testFilePath", this._geoUtilsMock.Object, this._fsMock.Object);

            var res = useCoords ? fileClient.GetTile(cords) : fileClient.GetTile(cords.Z, cords.X, cords.Y);
            if (returnsNull)
            {
                Assert.IsNull(res);
            }
            else
            {
                Assert.AreEqual(cords.X, res.X);
                Assert.AreEqual(cords.Y, res.Y);
                Assert.AreEqual(cords.Z, res.Z);
                Assert.AreEqual(targetFormat, res.Format);
                CollectionAssert.AreEqual(data, res.GetImageBytes());
            }
            this._repository.VerifyAll();
        }

        // GetTilePath probes jpeg then png via File.Exists. Sets up only the calls the probe actually
        // makes: png is not probed once jpeg is found, so its setups are omitted (strict mocks).
        private void SetupTilePathProbe(Coord cords, bool missing, TileFormat targetFormat, out string? foundPath)
        {
            string jpegPath = "1/2/3.jpeg";
            string pngPath = "1/2/3.png";
            bool jpegExists = !missing && targetFormat == TileFormat.Jpeg;
            bool pngExists = !missing && targetFormat == TileFormat.Png;

            this._pathMock
                .Setup(p => p.Combine("testFilePath", cords.Z.ToString(), cords.X.ToString(), $"{cords.Y}.jpeg"))
                .Returns(jpegPath);
            this._fileMock.Setup(f => f.Exists(jpegPath)).Returns(jpegExists);

            if (!jpegExists)
            {
                this._pathMock
                    .Setup(p => p.Combine("testFilePath", cords.Z.ToString(), cords.X.ToString(), $"{cords.Y}.png"))
                    .Returns(pngPath);
                this._fileMock.Setup(f => f.Exists(pngPath)).Returns(pngExists);
            }

            foundPath = jpegExists ? jpegPath : (pngExists ? pngPath : null);
        }

        #endregion

        #region TileExists

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void TileExists(bool exist)
        {
            Coord cords = new Coord(1, 2, 3);

            SetupTilePathProbe(cords, !exist, TileFormat.Jpeg, out _);

            var fileClient = new FileClient("testFilePath", this._geoUtilsMock.Object, this._fsMock.Object);

            var res = fileClient.TileExists(cords.Z, cords.X, cords.Y);

            Assert.AreEqual(exist, res);
            this._repository.VerifyAll();
        }

        #endregion

    }
}

