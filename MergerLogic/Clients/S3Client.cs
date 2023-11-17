using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MergerLogic.Clients
{
    public class S3Client : DataUtils, IS3Client
    {
        private string _bucket;
        private readonly IAmazonS3 _client;
        private readonly ILogger _logger;
        private readonly IPathUtils _pathUtils;

        public S3Client(IAmazonS3 client, IPathUtils pathUtils, IGeoUtils geoUtils, IImageFormatter formatter, ILogger<S3Client> logger, string bucket, string path) : base(path, geoUtils, formatter)
        {
            this._client = client ?? throw new Exception("s3 configuration is required");
            this._pathUtils = pathUtils;
            this._logger = logger;
            this._bucket = bucket;
        }

        public string Bucket
        {
            get { return this._bucket; }
        }

        private byte[]? GetImageBytes(string key)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            try
            {
                this._logger.LogDebug($"[{methodName}] start, key {key}");
                var request = new GetObjectRequest() { BucketName = this._bucket, Key = key };
                var getObjectTask = this._client.GetObjectAsync(request);
                GetObjectResponse res = getObjectTask.Result;

                byte[] image;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (var responseStream = res.ResponseStream)
                    {
                        responseStream.CopyTo(ms);
                    }

                    image = ms.ToArray();
                }
                this._logger.LogDebug($"[{methodName}] end, key {key}");
                return image;
            }
            catch (AggregateException e)
            {
                string message = $"exception while getting key {key}, Message: {e.Message}";
                this._logger.LogError($"[{methodName}] {message}");
                throw new Exception(message, e);
            }
        }

        public override Tile? GetTile(int z, int x, int y)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            this._logger.LogDebug($"[{methodName}] start z: {z}, x: {x}, y: {y}");
            var key = this.GetTileKey(z, x, y);
            if (key == null)
            {
                this._logger.LogDebug($"[{methodName}] tileKey is null for z: {z}, x: {x}, y: {y}");
                return null;
            }

            byte[]? imageBytes = this.GetImageBytes(key);
            this._logger.LogDebug($"[{methodName}] end z: {z}, x: {x}, y: {y}");
            return this.createTile(z, x, y, imageBytes);
        }

        public Tile? GetTile(string key)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            this._logger.LogDebug($"[{methodName}] start key: {key}");
            Coord coords = this._pathUtils.FromPath(key, out TileFormat format, true);
            byte[]? imageBytes = this.GetImageBytes(key);
            if (imageBytes == null)
            {
                return null;
            }
            this._logger.LogDebug($"[{methodName}] end key: {key}");
            return new Tile(coords, imageBytes, format);
        }

        public override bool TileExists(int z, int x, int y)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            this._logger.LogDebug($"[{methodName}] start z: {z}, x: {x}, y: {y}");
            bool isExists = this.GetTileKey(z, x, y) != null;
            this._logger.LogDebug($"[{methodName}] end z: {z}, x: {x}, y: {y}");
            return isExists;
        }

        public void UpdateTile(Tile tile)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            this._logger.LogDebug($"[{methodName}] start {tile.ToString()}");
            string key = this._pathUtils.GetTilePath(this.path, tile, true);

            var request = new PutObjectRequest()
            {
                BucketName = this._bucket,
                CannedACL = S3CannedACL.PublicRead,
                Key = String.Format(key)
            };

            byte[] buffer = tile.GetImageBytes();
            using (var ms = new MemoryStream(buffer))
            {
                request.InputStream = ms;
                this._logger.LogDebug($"[{methodName}] start PutObjectAsync BucketName: {request.BucketName}, Key: {request.Key}");
                var task = this._client.PutObjectAsync(request);
                var res = task.Result;
            }
            this._logger.LogDebug($"[{methodName}] end {tile.ToString()}");
        }

        private string? GetTileKey(int z, int x, int y)
        {
            string keyPrefix = this._pathUtils.GetTilePathWithoutExtension(this.path, z, x, y, true);
            var listRequests = new ListObjectsV2Request { BucketName = this._bucket, Prefix = keyPrefix, MaxKeys = 1 };
            var listObjectsTask = this._client.ListObjectsV2Async(listRequests);
            string? result = listObjectsTask.Result.S3Objects.FirstOrDefault()?.Key;
            return result;
        }

        public ListObjectsV2Response ListObject(ref string? continuationToken, string prefix, string startAfter, int? maxKeys = null)
        {
            ListObjectsV2Request listRequests = null;
            if (maxKeys == null)
            {
                listRequests = new ListObjectsV2Request
                {
                    BucketName = this._bucket,
                    Prefix = prefix,
                    StartAfter = startAfter,
                    ContinuationToken = continuationToken
                };
            }
            else
            {
                listRequests = new ListObjectsV2Request
                {
                    BucketName = this._bucket,
                    Prefix = prefix,
                    StartAfter = startAfter,
                    ContinuationToken = continuationToken,
                    MaxKeys = maxKeys.Value
                };
            }

            var task = this._client.ListObjectsV2Async(listRequests);
            var response = task.Result;
            return response;
        }
    }
}
