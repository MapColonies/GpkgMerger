using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;

namespace MergerLogic.Clients
{
    public class S3Client : DataUtils, IS3Client
    {
        private readonly string _bucket;
        private readonly IAmazonS3 _client;
        private readonly ILogger _logger;
        private readonly IPathUtils _pathUtils;
        private readonly S3StorageClass _storageClass;

        public S3Client(IAmazonS3 client, IPathUtils pathUtils, IGeoUtils geoUtils, ILogger<S3Client> logger,
            string storageClass, string bucket, string path) : base(path, geoUtils)
        {
            this._client = client;
            this._bucket = bucket;
            this._pathUtils = pathUtils;
            this._logger = logger;
            // There is no validation on the storage class, only on PUT object we can know if the given class is supported
            this._storageClass = new S3StorageClass(storageClass ?? S3StorageClass.Standard);
        }

        // Read paths don't know a tile's extension ahead of time, so existence is probed against
        // these candidates in order (matches the historical Jpeg-then-Png GetTile lookup).
        private static readonly TileFormat[] _readFormats = { TileFormat.Jpeg, TileFormat.Png };

        private bool IsKeyError(Exception e)
        {
            if (e is AmazonS3Exception ex)
            {
                return ex.ErrorCode == "NoSuchKey";
            }

            if (e.InnerException is AmazonS3Exception en)
            {
                return en.ErrorCode == "NoSuchKey";
            }

            return false;
        }

        // HEAD returns 404 (not the GET "NoSuchKey") for a missing object.
        private bool IsKeyNotFound(Exception e)
        {
            AmazonS3Exception? ex = e as AmazonS3Exception ?? e.InnerException as AmazonS3Exception;
            if (ex is null)
            {
                return false;
            }

            return ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchKey";
        }

        private byte[]? GetImageBytes(string key)
        {
            string? methodName = MethodBase.GetCurrentMethod()?.Name;
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
                if (IsKeyError(e))
                {
                    string message = $"exception while getting key {key}, Message: {e.Message}";
                    this._logger.LogDebug($"[{methodName}] {message}");
                    return null;
                }
                // In case there are other errors such as connection to S3
                throw e;
            }
        }

        public override Tile? GetTile(int z, int x, int y)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            this._logger.LogDebug($"[{methodName}] start z: {z}, x: {x}, y: {y}");
            string keyPrefix = this._pathUtils.GetTilePath(this.path, z, x, y, TileFormat.Jpeg, true);

            byte[]? imageBytes = this.GetImageBytes(keyPrefix);
            if (imageBytes == null)
            {
                keyPrefix = this._pathUtils.GetTilePath(this.path, z, x, y, TileFormat.Png, true);
                imageBytes = this.GetImageBytes(keyPrefix);
                if (imageBytes == null)
                {
                    return null;
                }
            }

            this._logger.LogDebug($"[{methodName}] end z: {z}, x: {x}, y: {y}");
            return this.CreateTile(z, x, y, imageBytes);
        }

        public Tile? GetTile(string key)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            this._logger.LogDebug($"[{methodName}] start key: {key}");
            byte[]? imageBytes = this.GetImageBytes(key);
            if (imageBytes == null)
            {
                return null;
            }
            
            this._logger.LogDebug($"[{methodName}] end key: {key}");
            Coord coords = this._pathUtils.FromPath(key, true);
            return this.CreateTile(coords, imageBytes);
        }

        public override bool TileExists(int z, int x, int y)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            this._logger.LogDebug($"[{methodName}] start z: {z}, x: {x}, y: {y}");
            bool exists = this.GetTileKey(z, x, y) != null;
            this._logger.LogDebug($"[{methodName}] end z: {z}, x: {x}, y: {y}");
            return exists;
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
                Key = String.Format(key),
                StorageClass = this._storageClass
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

        // Resolves existence and the real extension with a HEAD per candidate format. HEAD is an
        // O(1) key lookup; a prefix LIST would scan the bucket index, which does not scale on the
        // billions-of-objects bucket (MAPCO-7954).
        private string? GetTileKey(int z, int x, int y)
        {
            foreach (TileFormat format in _readFormats)
            {
                string key = this._pathUtils.GetTilePath(this.path, z, x, y, format, true);
                if (this.KeyExists(key))
                {
                    return key;
                }
            }

            return null;
        }

        private bool KeyExists(string key)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            try
            {
                var request = new GetObjectMetadataRequest { BucketName = this._bucket, Key = key };
                this._logger.LogDebug($"[{methodName}] GetObjectMetadataAsync BucketName: {this._bucket}, Key: {key}");
                var task = this._client.GetObjectMetadataAsync(request);
                _ = task.Result;
                return true;
            }
            catch (AggregateException e)
            {
                if (IsKeyNotFound(e))
                {
                    this._logger.LogDebug($"[{methodName}] key not found: {key}");
                    return false;
                }
                // In case there are other errors such as connection to S3
                throw e;
            }
        }
    }
}
