using MergerLogic.Clients;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;

namespace MergerLogicUnitTests.Clients
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("fs")]
    [TestCategory("FileClient")]
    public class FileClientTest 
    {
        #region mocks
        private MockRepository _repository;
        private Mock<IPathUtils> _pathUtilsMock;
        private Mock<IGeoUtils> _geoUtilsMock;
        private Mock<IFileSystem> _fsMock;
        private Mock<IFile> _fileMock;
        private Mock<IImageFormatter> _imageFormatterMock;
        #endregion

        [TestInitialize]
        public void beforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._pathUtilsMock = this._repository.Create<IPathUtils>();
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();
            this._fsMock = this._repository.Create<IFileSystem>();
            this._fileMock = this._repository.Create<IFile>();
            this._fsMock.SetupGet(fs => fs.File).Returns(this._fileMock.Object);
            this._imageFormatterMock = this._repository.Create<IImageFormatter>();
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
        [DynamicData(nameof(GetGetTileParams),DynamicDataSourceType.Method)]
        public void GetTile(bool useCoords, bool returnsNull, TileFormat targetFormat)
        {
            Coord cords = new Coord(1, 2, 3);
            byte[] data = Array.Empty<byte>();

            var seq = new MockSequence();
            this._pathUtilsMock.InSequence(seq).Setup(util => util.GetTilePath("testFilePath",cords.Z,cords.X,cords.Y,targetFormat,false))
                .Returns("testTilePath");
            this._fileMock.InSequence(seq).Setup(util => util.Exists("testTilePath"))
                .Returns(!returnsNull);
            if (!returnsNull)
            {
                this._fileMock.InSequence(seq).Setup(util => util.ReadAllBytes("testTilePath"))
                    .Returns(data);
            }

            var fileClient = new FileClient("testFilePath", this._pathUtilsMock.Object,
                this._geoUtilsMock.Object,this._fsMock.Object, this._imageFormatterMock.Object);

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
            byte[] data = Array.Empty<byte>();

            var seq = new MockSequence();
            this._pathUtilsMock.InSequence(seq).Setup(util => util.GetTilePathWithoutExtension("testFilePath",cords.Z,cords.X,cords.Y, false))
                .Returns("testTilePath");
            this._fileMock.InSequence(seq).Setup(util => util.Exists("testTilePath"))
                .Returns(exist);

            var fileClient = new FileClient("testFilePath", this._pathUtilsMock.Object,
                this._geoUtilsMock.Object,this._fsMock.Object, this._imageFormatterMock.Object);

            var res = fileClient.TileExists(cords.Z, cords.X, cords.Y);

           Assert.AreEqual(exist,res);
            this._repository.VerifyAll();
        }

        #endregion

    }
}

