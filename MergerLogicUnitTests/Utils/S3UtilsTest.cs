using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
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
using System.Threading;
using System.Threading.Tasks;

namespace MergerLogicUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("S3")]
    [TestCategory("s3Utils")]
    public class S3UtilsTest
    {
        #region mocks

        private MockRepository _repository;
        private Mock<IAmazonS3> _s3ClientMock;
        private Mock<IPathUtils> _pathUtilsMock;
        private Mock<IGeoUtils> _geoUtilsMock;
        private Mock<IImageFormatter> _imageFormatterMock;

        #endregion

        [TestInitialize]
        public void BeforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._s3ClientMock = this._repository.Create<IAmazonS3>();
            this._pathUtilsMock = this._repository.Create<IPathUtils>();
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();
            this._imageFormatterMock = this._repository.Create<IImageFormatter>();
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
        public void GetTile(bool exist, GetTileParamType paramType,TileFormat tileFormat)
        {
            var seq = new MockSequence();
            var data = new byte[1];
            var cords = new Coord(0, 0, 0);
            using (var dataStream = new MemoryStream(data))
            {
                if (paramType == GetTileParamType.String)
                {
                    this._pathUtilsMock
                        .InSequence(seq)
                        .Setup(utils => utils.FromPath("key", out tileFormat, true))
                        .Returns(cords);
                }
                else
                {
                    this._pathUtilsMock
                        .InSequence(seq)
                        .Setup(utils => utils.GetTilePath("test", cords.Z, cords.X, cords.Y, tileFormat, true))
                        .Returns("key");
                }

                if (exist)
                {
                    this._s3ClientMock
                        .InSequence(seq)
                        .Setup(s3 => s3.GetObjectAsync(It.Is<GetObjectRequest>(req =>
                            req.BucketName == "bucket" && req.Key == "key"), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new GetObjectResponse() { ResponseStream = dataStream });
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
                    this._geoUtilsMock.Object, this._imageFormatterMock.Object, "bucket", "test");

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
                        tile = s3Utils.GetTile("key");
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
                this._pathUtilsMock.Verify(utils => utils.FromPath(It.IsAny<string>(), out It.Ref<TileFormat>.IsAny, It.IsAny<bool>()), Times.Once);
            }
            else
            {
                this._pathUtilsMock.Verify(utils => utils.GetTilePath(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<TileFormat>(), It.IsAny<bool>()), Times.Once);
            }

            this._s3ClientMock.Verify(s3 =>
                s3.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
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
            if (exist)
            {
                this._s3ClientMock
                    .InSequence(seq)
                    .Setup(s3 => s3.GetObjectMetadataAsync(It.Is<GetObjectMetadataRequest>(req =>
                        req.BucketName == "bucket" && req.Key == "key"), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GetObjectMetadataResponse());
            }
            else
            {
                this._s3ClientMock
                    .InSequence(seq)
                    .Setup(s3 => s3.GetObjectMetadataAsync(It.Is<GetObjectMetadataRequest>(req =>
                        req.BucketName == "bucket" && req.Key == "key"), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Amazon.S3.AmazonS3Exception("test"));
            }

            var s3Utils = new S3Client(this._s3ClientMock.Object, this._pathUtilsMock.Object,
                this._geoUtilsMock.Object, this._imageFormatterMock.Object, "bucket", "test");

            Assert.AreEqual(exist, s3Utils.TileExists(0, 0, 0));

            this._pathUtilsMock.Verify(utils => utils.GetTilePathWithoutExtension("test", 0, 0, 0, true), Times.Once);
            this._s3ClientMock.Verify(s3 => s3.GetObjectMetadataAsync(It.Is<GetObjectMetadataRequest>(req =>
                        req.BucketName == "bucket" && req.Key == "key"), It.IsAny<CancellationToken>()), Times.Once);
            this.VerifyAll();
        }

        #endregion

        #region UpdateTile

        [TestMethod]
        [TestCategory("UpdateTile")]
        public void UpdateTile()
        {
            var buff = new byte[10];
            int readLen = -1;
            var seq = new MockSequence();
            this._pathUtilsMock
                .InSequence(seq)
                .Setup(utils => utils.GetTilePathWithoutExtension("test", 0, 0, 0, true))
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
                this._geoUtilsMock.Object, this._imageFormatterMock.Object, "bucket", "test");
            s3Utils.UpdateTile(new Tile(0, 0, 0, new byte[1]));

            this._pathUtilsMock.Verify(utils => utils.GetTilePathWithoutExtension("test", 0, 0, 0, true), Times.Once);
            this._s3ClientMock.Verify(s3 => s3.PutObjectAsync(It.Is<PutObjectRequest>(req =>
                req.BucketName == "bucket" && req.Key == "key"), It.IsAny<CancellationToken>()), Times.Once);

            Assert.AreEqual(1, readLen);
            Assert.AreEqual(0, buff[0]);
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
