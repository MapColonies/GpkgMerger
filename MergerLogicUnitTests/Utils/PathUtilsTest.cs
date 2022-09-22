using MergerLogic.Batching;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;

namespace MergerLogicUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("PathUtils")]
    public class PathUtilsTest
    {
        #region mocks

        private MockRepository _repository;
        private Mock<IFileSystem> _fileSystemMock;
        private Mock<IPath> _pathMock;
        private Mock<IImageFormatter> _imageFormaterMock;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._pathMock = this._repository.Create<IPath>();
            this._pathMock.SetupGet(path => path.DirectorySeparatorChar).Returns('#');//test separator is used to distinguish from s3 separator
            this._fileSystemMock = this._repository.Create<IFileSystem>();
            this._fileSystemMock.SetupGet(fs => fs.Path).Returns(this._pathMock.Object);
            this._imageFormaterMock = this._repository.Create<IImageFormatter>();
        }

        #region RemoveTrailingSlash

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void RemoveTrailingSlash(bool isS3)
        {
            var utils = new PathUtils(this._fileSystemMock.Object,this._imageFormaterMock.Object);
            var expectedPath = "22#222#sdgsdgtw";
            string testPath;
            if (isS3)
            {
                expectedPath = expectedPath.Replace('#', '/');
                testPath = expectedPath + "/";
            }
            else
            {
                testPath = expectedPath + "#";
            }

            var res = utils.RemoveTrailingSlash(testPath, isS3);
            Assert.AreEqual(expectedPath, res);
        }

        #endregion

        #region GetTilePath

        [TestMethod]
        [DataRow(TileFormat.Jpeg)]
        [DataRow(TileFormat.Png)]
        public void GetTilePathTile(TileFormat format)
        {
            var utils = new PathUtils(this._fileSystemMock.Object,this._imageFormaterMock.Object);
            var res = utils.GetTilePath("test#subTest", new Tile(0, 1, 2, Array.Empty<byte>(),format));
            Assert.AreEqual($"test#subTest#0#1#2.{format.ToString().ToLower()}", res);
        }

        public static IEnumerable<object[]> GenGetTilePathCoordsParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
            {
                new object[] { true, false }, // isS3
                new object[] { TileFormat.Png, TileFormat.Jpeg } // tile format
            });
        }

        [TestMethod]
        [DynamicData(nameof(GenGetTilePathCoordsParams),DynamicDataSourceType.Method)]
        public void GetTilePathCoords(bool isS3, TileFormat format)
        {
            var utils = new PathUtils(this._fileSystemMock.Object,this._imageFormaterMock.Object);

            var testPath = "test#subTest";
            var expected = $"test#subTest#0#1#2.{format.ToString().ToLower()}";
            if (isS3)
            {
                testPath = testPath.Replace('#', '/');
                expected = expected.Replace('#', '/');
            }

            var res = utils.GetTilePath(testPath, 0, 1, 2, format, isS3);

            Assert.AreEqual(expected, res);
        }

        #endregion

        #region FromPath

        public static IEnumerable<object[]> GenFromPathParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
            {
                new object[] { true, false }, // isS3
                new object[] { TileFormat.Png, TileFormat.Jpeg } // tile format
            });
        }

        [TestMethod]
        [DynamicData(nameof(GenFromPathParams),DynamicDataSourceType.Method)]
        public void FromPath(bool isS3, TileFormat expectedFormat)
        {
            var utils = new PathUtils(this._fileSystemMock.Object,this._imageFormaterMock.Object);

            var testPath = $"test#subTest#0#1#2.{expectedFormat.ToString().ToLower()}";
            if (isS3)
            {
                testPath = testPath.Replace('#', '/');
            }

            var res = utils.FromPath(testPath,out TileFormat format, isS3);

            Assert.AreEqual(0, res.Z);
            Assert.AreEqual(1, res.X);
            Assert.AreEqual(2, res.Y);
            Assert.AreEqual(expectedFormat,format);
        }

        #endregion
    }
}
