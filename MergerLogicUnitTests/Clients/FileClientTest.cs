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
        private Mock<IConfigurationManager> _configurationManagerMock;
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
            this._configurationManagerMock = this._repository.Create<IConfigurationManager>();
            this._fsMock.SetupGet(fs => fs.File).Returns(this._fileMock.Object);
            this._fsMock.SetupGet(fs => fs.Path).Returns(this._pathMock.Object);
            this._fsMock.SetupGet(fs => fs.Directory).Returns(this._directoryMock.Object);
            this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<long>("GENERAL", "allowedPixelSize"))
                .Returns(256);
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

            var seq = new MockSequence();
            this._pathMock
                .InSequence(seq)
                .Setup(util => util.Join(cords.Z.ToString(), cords.X.ToString(), cords.Y.ToString()))
                .Returns("testTilePath");
            this._directoryMock
                .InSequence(seq)
                .Setup(dir => dir.EnumerateFiles("testFilePath", "testTilePath.*", SearchOption.TopDirectoryOnly))
                .Returns(returnsNull ? Array.Empty<string>() : new string[] { "testTilePath" });
            if (!returnsNull)
            {
                this._fileMock
                    .InSequence(seq)
                    .Setup(util => util.ReadAllBytes("testTilePath"))
                    .Returns(data);
                this._imageFormatterMock
                    .InSequence(seq)
                    .Setup(formatter => formatter.GetTileFormat(data))
                    .Returns(targetFormat);
            }

            var fileClient = new FileClient("testFilePath", this._geoUtilsMock.Object, this._fsMock.Object, this._configurationManagerMock.Object);

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

        #endregion

        #region TileExists

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void TileExists(bool exist)
        {
            Coord cords = new Coord(1, 2, 3);
            byte[] data = this._jpegImageData;

            var seq = new MockSequence();
            this._pathMock
                .InSequence(seq)
                .Setup(util => util.Join(cords.Z.ToString(), cords.X.ToString(), cords.Y.ToString()))
                .Returns("testTilePath");
            this._directoryMock
                .InSequence(seq)
                .Setup(dir => dir.EnumerateFiles("testFilePath", "testTilePath.*", SearchOption.TopDirectoryOnly))
                .Returns(exist ? new string[] { "testFile" } : Array.Empty<string>());

            var fileClient = new FileClient("testFilePath", this._geoUtilsMock.Object, this._fsMock.Object, this._configurationManagerMock.Object);

            var res = fileClient.TileExists(cords.Z, cords.X, cords.Y);

            Assert.AreEqual(exist, res);
            this._repository.VerifyAll();
        }

        [TestMethod]
        public void TileExistsReturnFalseWhenDirectoryDontExist()
        {
            Coord cords = new Coord(1, 2, 3);

            var seq = new MockSequence();
            this._pathMock
                .InSequence(seq)
                .Setup(util => util.Join(cords.Z.ToString(), cords.X.ToString(), cords.Y.ToString()))
                .Returns("testTilePath");
            this._directoryMock
                .InSequence(seq)
                .Setup(dir => dir.EnumerateFiles("testFilePath", "testTilePath.*", SearchOption.TopDirectoryOnly))
                .Throws<DirectoryNotFoundException>();

            var fileClient = new FileClient("testFilePath", this._geoUtilsMock.Object, this._fsMock.Object, this._configurationManagerMock.Object);

            var res = fileClient.TileExists(cords.Z, cords.X, cords.Y);

            Assert.AreEqual(false, res);
            this._repository.VerifyAll();
        }

        #endregion

    }
}

