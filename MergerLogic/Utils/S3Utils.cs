using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;

namespace MergerLogic.Utils
{
    public class S3Utils : DataUtils, IS3Utils
    {
        private string bucket;

        private IAmazonS3 client;
        private IPathUtils _pathUtils;

        public S3Utils(IAmazonS3 client, IPathUtils pathUtils, string bucket, string path) : base(path)
        {
            this.client = client;
            this.bucket = bucket;
            this._pathUtils = pathUtils;
        }

        private byte[] GetImageBytes(string key)
        {
            try
            {
                var request = new GetObjectRequest()
                {
                    BucketName = bucket,
                    Key = key
                };
                var getObjectTask = this.client.GetObjectAsync(request);
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
            catch (AggregateException e)
            {
                // Console.WriteLine($"Error getting tile (key={key}): {e.Message}");
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

        public override bool TileExists(int z, int x, int y)
        {
            string key = this._pathUtils.GetTilePath(this.path, z, x, y, true);
            var request = new GetObjectMetadataRequest()
            {
                BucketName = this.bucket,
                Key = String.Format(key)
            };

            try
            {
                var task = this.client.GetObjectMetadataAsync(request);
                _ = task.Result;
                return true;
            }
            catch (AmazonS3Exception e)
            {
                return false;
            }
        }

        public void UpdateTile(Tile tile)
        {
            string key = this._pathUtils.GetTilePath(this.path, tile.Z, tile.X, tile.Y, true);

            var request = new PutObjectRequest()
            {
                BucketName = this.bucket,
                CannedACL = S3CannedACL.PublicRead,
                Key = String.Format(key)
            };

            byte[] buffer = tile.GetImageBytes();
            using (var ms = new MemoryStream(buffer))
            {
                request.InputStream = ms;
                var task = this.client.PutObjectAsync(request);
                var res = task.Result;
            }
        }
    }
}
