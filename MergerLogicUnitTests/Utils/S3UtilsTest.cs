using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MergerLogicUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("S3")]
    [TestCategory("s3Utils")]
    [DeploymentItem(@"../../../Utils/TestData")]
    public class S3UtilsTest
    {
        #region mocks

        private MockRepository _repository;
        private Mock<IAmazonS3> _s3ClientMock;
        private Mock<IPathUtils> _pathUtilsMock;
        private Mock<IGeoUtils> _geoUtilsMock;
        private Mock<IImageFormatter> _imageFormatterMock;
        private Mock<ILogger<S3Client>> _loggerMock;
        private Mock<ILoggerFactory> _loggerFactoryMock;

        private byte[] _jpegImageData;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._s3ClientMock = this._repository.Create<IAmazonS3>();
            this._pathUtilsMock = this._repository.Create<IPathUtils>();
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();
            this._imageFormatterMock = this._repository.Create<IImageFormatter>();

            this._loggerMock = this._repository.Create<ILogger<S3Client>>(MockBehavior.Loose);
            this._loggerFactoryMock = this._repository.Create<ILoggerFactory>();
            this._loggerFactoryMock.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(this._loggerMock.Object);

            this._jpegImageData = File.ReadAllBytes("no_transparency.jpeg");
        }

        #region GetTile
        public enum GetTileParamType { String, Coord, Ints }

        public static IEnumerable<object[]> GenGetTileParams()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
            {
                new object[] { true, false }, // exist
                new object[] { GetTileParamType.Coord, GetTileParamType.Ints, GetTileParamType.String }, //get tile overload
                new object[] { TileFormat.Png, TileFormat.Jpeg } // tile format
            });
        }

        [TestMethod]
        [TestCategory("GetTile")]
        [DynamicData(nameof(GenGetTileParams), DynamicDataSourceType.Method)]
        public void GetTile(bool exist, GetTileParamType paramType, TileFormat tileFormat)
        {
            var seq = new MockSequence();
            var data = this._jpegImageData;
            var cords = new Coord(0, 0, 0);

            if (paramType == GetTileParamType.String)
            {
                this._pathUtilsMock
                    .InSequence(seq)
                    .Setup(utils => utils.FromPath("key", true))
                    .Returns(cords);
            }
            else
            {
                var keyPrefix = $"test/{cords.Z}/{cords.X}/{cords.Y}";
                this._pathUtilsMock
                    .InSequence(seq)
                    .Setup(utils =>
                        utils.GetTilePathWithoutExtension("test", cords.Z, cords.X, cords.Y, true))
                    .Returns(keyPrefix);
                this._s3ClientMock
                    .InSequence(seq)
                    .Setup(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(req =>
                            req.BucketName == "bucket" && req.Prefix == keyPrefix && req.MaxKeys == 1),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ListObjectsV2Response()
                    {
                        S3Objects = exist ? new List<S3Object>() { new S3Object() { Key = "key" } } : new List<S3Object>()
                    });
            }

            using (var dataStream = new MemoryStream(data))
            {
                if (exist)
                {
                    this._s3ClientMock
                        .InSequence(seq)
                        .Setup(s3 => s3.GetObjectAsync(It.Is<GetObjectRequest>(req =>
                            req.BucketName == "bucket" && req.Key == "key"), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new GetObjectResponse() { ResponseStream = dataStream });
                    this._imageFormatterMock.Setup(formatter => formatter.GetTileFormat(It.IsAny<byte[]>()))
                        .Returns(tileFormat);
                }
                else
                {
                    this._s3ClientMock
                        .InSequence(seq)
                        .Setup(s3 => s3.GetObjectAsync(It.Is<GetObjectRequest>(req =>
                            req.BucketName == "bucket" && req.Key == "key"), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new AggregateException("test"));
                }

                var s3Utils = new S3Client(this._s3ClientMock.Object, this._pathUtilsMock.Object,
                    this._geoUtilsMock.Object, this._loggerMock.Object, "STANDARD", "bucket", "test");

                Tile tile = null;
                switch (paramType)
                {
                    case GetTileParamType.Coord:
                        tile = s3Utils.GetTile(cords);
                        break;
                    case GetTileParamType.Ints:
                        tile = s3Utils.GetTile(cords.Z, cords.X, cords.Y);
                        break;
                    case GetTileParamType.String:
                        if (exist)
                        {
                            tile = s3Utils.GetTile("key");
                        }
                        else
                        {
                            Assert.ThrowsException<Exception>(() => s3Utils.GetTile("key"));
                            tile = null;
                        }
                        break;
                }

                if (!exist)
                {
                    Assert.IsNull(tile);
                }
                else
                {
                    Assert.AreEqual(cords.Z, tile.Z);
                    Assert.AreEqual(cords.X, tile.X);
                    Assert.AreEqual(cords.Y, tile.Y);
                    CollectionAssert.AreEqual(data, tile.GetImageBytes());
                }
            }
            if (paramType == GetTileParamType.String)
            {
                this._pathUtilsMock.Verify(utils => utils.FromPath(It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
            }
            else
            {
                this._pathUtilsMock.Verify(utils => utils.GetTilePathWithoutExtension(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<bool>()), Times.Once);
                this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            if (exist || paramType == GetTileParamType.String)
            {
                this._s3ClientMock.Verify(s3 =>
                    s3.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
            }

            this.VerifyAll();
        }

        #endregion

        #region TileExists

        [TestMethod]
        [TestCategory("TileExists")]
        [DataRow(true)]
        [DataRow(false)]
        public void TileExists(bool exist)
        {
            var seq = new MockSequence();
            this._pathUtilsMock
                        .InSequence(seq)
                        .Setup(utils => utils.GetTilePathWithoutExtension("test", 0, 0, 0, true))
                        .Returns("key");
            this._s3ClientMock
                .InSequence(seq)
                .Setup(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(req =>
                        req.BucketName == "bucket" && req.Prefix == "key" && req.MaxKeys == 1),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ListObjectsV2Response()
                {
                    S3Objects = exist ? new List<S3Object>() { new S3Object() { Key = "key" } } : new List<S3Object>()
                });

            var s3Utils = new S3Client(this._s3ClientMock.Object, this._pathUtilsMock.Object,
                this._geoUtilsMock.Object, this._loggerMock.Object, "STANDARD", "bucket", "test");

            Assert.AreEqual(exist, s3Utils.TileExists(0, 0, 0));

            this._pathUtilsMock.Verify(utils => utils.GetTilePathWithoutExtension("test", 0, 0, 0, true), Times.Once);
            this._s3ClientMock.Verify(s3 => s3.ListObjectsV2Async(It.Is<ListObjectsV2Request>(req =>
                        req.BucketName == "bucket" && req.Prefix == "key"), It.IsAny<CancellationToken>()), Times.Once);
            this.VerifyAll();
        }

        #endregion

        #region UpdateTile

        [TestMethod]
        [TestCategory("UpdateTile")]
        public void UpdateTile()
        {
            var buff = this._jpegImageData;
            int readLen = -1;
            var seq = new MockSequence();
            var testTile = new Tile(0, 0, 0, buff);
            this._pathUtilsMock
                .InSequence(seq)
                .Setup(utils => utils.GetTilePath("test", testTile, true))
                .Returns("key");
            this._s3ClientMock
                .InSequence(seq)
                .Setup(s3 => s3.PutObjectAsync(It.Is<PutObjectRequest>(req =>
                    req.BucketName == "bucket" && req.Key == "key"), It.IsAny<CancellationToken>()))
                .Returns<PutObjectRequest, CancellationToken>((req, _) =>
                {
                    readLen = req.InputStream.Read(buff);
                    return Task.FromResult(new PutObjectResponse());
                });

            var s3Utils = new S3Client(this._s3ClientMock.Object, this._pathUtilsMock.Object,
                this._geoUtilsMock.Object, this._loggerMock.Object, "STANDARD", "bucket", "test");
            s3Utils.UpdateTile(testTile);

            this._pathUtilsMock.Verify(utils => utils.GetTilePath(It.IsAny<string>(), It.IsAny<Tile>(), It.IsAny<bool>()), Times.Once);
            this._s3ClientMock.Verify(s3 => s3.PutObjectAsync(It.Is<PutObjectRequest>(req =>
                req.BucketName == "bucket" && req.Key == "key"), It.IsAny<CancellationToken>()), Times.Once);

            Assert.AreEqual(buff.Length, readLen);
            Assert.AreEqual(this._jpegImageData[0], buff[0]);
            this.VerifyAll();
        }

        #endregion

        #region helper

        private void VerifyAll()
        {
            this._s3ClientMock.VerifyAll();
            this._pathUtilsMock.VerifyAll();
            this._geoUtilsMock.VerifyAll();
        }

        #endregion
    }
}
