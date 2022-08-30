using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;

namespace MergerLogic.Clients
{
    public class S3Client : DataUtils, IS3Client
    {
        private readonly string _bucket;

        private readonly IAmazonS3 _client;
        private readonly IPathUtils _pathUtils;

        public S3Client(IAmazonS3 client, IPathUtils pathUtils, IGeoUtils geoUtils, string bucket, string path) : base(path, geoUtils)
        {
            this._client = client;
            this._bucket = bucket;
            this._pathUtils = pathUtils;
        }

        private byte[] GetImageBytes(string key)
        {
            try
            {
                var request = new GetObjectRequest()
                {
                    BucketName = this._bucket,
                    Key = key
                };
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

        public override Tile GetTile(int z, int x, int y)
        {
            string key = this._pathUtils.GetTilePath(this.path, z, x, y, true);
            byte[]? imageBytes = this.GetImageBytes(key);
            if (imageBytes == null)
            {
                return null;
            }
            return new Tile(z, x, y, imageBytes);
        }

        public Tile GetTile(string key)
        {
            Coord coords = this._pathUtils.FromPath(key, true);
            byte[]? imageBytes = this.GetImageBytes(key);
            if (imageBytes == null)
            {
                return null;
            }
            return new Tile(coords, imageBytes);
        }

        public override bool TileExists(int z, int x, int y)
        {
            string key = this._pathUtils.GetTilePath(this.path, z, x, y, true);
            var request = new GetObjectMetadataRequest()
            {
                BucketName = this._bucket,
                Key = key
            };

            try
            {
                var task = this._client.GetObjectMetadataAsync(request);
                _ = task.Result;
                return true;
            }
            catch (AggregateException e)
            {
                return false;
            }
        }

        public void UpdateTile(Tile tile)
        {
            string key = this._pathUtils.GetTilePath(this.path, tile.Z, tile.X, tile.Y, true);

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
                var task = this._client.PutObjectAsync(request);
                var res = task.Result;
            }
        }
    }
}
