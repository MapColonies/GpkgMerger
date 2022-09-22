using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;

namespace MergerLogic.Clients
{
    public class S3Client : DataUtils, IS3Client
    {
        private readonly string _bucket;

        private readonly IAmazonS3 _client;
        private readonly IPathUtils _pathUtils;

        public S3Client(IAmazonS3 client, IPathUtils pathUtils, IGeoUtils geoUtils, IImageFormatter formatter,
            string bucket, string path) : base(path, geoUtils, formatter)
        {
            this._client = client;
            this._bucket = bucket;
            this._pathUtils = pathUtils;
        }

        private byte[] GetImageBytes(string key)
        {
            try
            {
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

                return image;
            }
            catch (AggregateException)
            {
                return null;
            }
        }

        public override Tile? GetTile(int z, int x, int y)
        {
            var key = this.GetTileKey(z, x, y);
            if (key == null)
            {
                return null;
            }

            byte[]? imageBytes = this.GetImageBytes(key);
            return this.createTile(z, x, y, imageBytes);
        }

        public Tile? GetTile(string key)
        {
            Coord coords = this._pathUtils.FromPath(key, out TileFormat format, true);
            byte[]? imageBytes = this.GetImageBytes(key);
            if (imageBytes == null)
            {
                return null;
            }

            return new Tile(coords, imageBytes, format);
        }

        public override bool TileExists(int z, int x, int y)
        {
            return this.GetTileKey(z, x, y) != null;
        }

        public void UpdateTile(Tile tile)
        {
            string key = this._pathUtils.GetTilePath(this.path, tile, true);

            var request = new PutObjectRequest()
            {
                BucketName = this._bucket, CannedACL = S3CannedACL.PublicRead, Key = String.Format(key)
            };

            byte[] buffer = tile.GetImageBytes();
            using (var ms = new MemoryStream(buffer))
            {
                request.InputStream = ms;
                var task = this._client.PutObjectAsync(request);
                var res = task.Result;
            }
        }

        private string? GetTileKey(int z, int x, int y)
        {
            string keyPrefix = this._pathUtils.GetTilePathWithoutExtension(this.path, z, x, y, true);
            var listRequests = new ListObjectsV2Request { BucketName = this._bucket, Prefix = keyPrefix, MaxKeys = 1 };

            var listObjectsTask = this._client.ListObjectsV2Async(listRequests);
            return listObjectsTask.Result.S3Objects.FirstOrDefault()?.Key;
        }
    }
}
