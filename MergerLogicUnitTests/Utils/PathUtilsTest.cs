using MergerLogic.Batching;
using MergerLogic.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
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

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._pathMock = this._repository.Create<IPath>();
            this._pathMock.SetupGet(path => path.DirectorySeparatorChar).Returns('#');//test separator is used to distinguish from s3 separator
            this._fileSystemMock = this._repository.Create<IFileSystem>();
            this._fileSystemMock.SetupGet(fs => fs.Path).Returns(this._pathMock.Object);
        }

        #region RemoveTrailingSlash

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void RemoveTrailingSlash(bool isS3)
        {
            var utils = new PathUtils(this._fileSystemMock.Object);
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
        public void GetTilePathTile()
        {
            var utils = new PathUtils(this._fileSystemMock.Object);
            var res = utils.GetTilePath("test#subTest", new Tile(0, 1, 2, Array.Empty<byte>()));
            Assert.AreEqual("test#subTest#0#1#2.png", res);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void GetTilePathCoords(bool isS3)
        {
            var utils = new PathUtils(this._fileSystemMock.Object);

            var testPath = "test#subTest";
            var expected = "test#subTest#0#1#2.png";
            if (isS3)
            {
                testPath = testPath.Replace('#', '/');
                expected = expected.Replace('#', '/');
            }

            var res = utils.GetTilePath(testPath, 0, 1, 2, isS3);

            Assert.AreEqual(expected, res);
        }

        #endregion

        #region FromPath

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void FromPath(bool isS3)
        {
            var utils = new PathUtils(this._fileSystemMock.Object);

            var testPath = "test#subTest#0#1#2.png";
            if (isS3)
            {
                testPath = testPath.Replace('#', '/');
            }

            var res = utils.FromPath(testPath, isS3);

            Assert.AreEqual(0, res.Z);
            Assert.AreEqual(1, res.X);
            Assert.AreEqual(2, res.Y);
        }

        #endregion
    }
}
