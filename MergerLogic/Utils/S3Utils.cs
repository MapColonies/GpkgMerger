using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;

namespace MergerLogic.Utils
{
    public class S3Utils : DataUtils
    {
        private AmazonS3Client client;
        private string bucket;

        public S3Utils(AmazonS3Client client, string bucket, string path) : base(path)
        {
            this.client = client;
            this.bucket = bucket;
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
            string key = PathUtils.GetTilePath(this.path, z, x, y, true);

            byte[]? imageBytes = this.GetImageBytes(key);
            if (imageBytes == null)
            {
                return null;
            }
            return new Tile(z, x, y, imageBytes);
        }

        public static void UpdateTile(AmazonS3Client client, string bucket, string path, Tile tile)
        {
            string key = PathUtils.GetTilePath(path, tile.Z, tile.X, tile.Y, true);

            var request = new PutObjectRequest()
            {
                BucketName = bucket,
                CannedACL = S3CannedACL.PublicRead,
                Key = String.Format(key)
            };

            byte[] buffer = tile.GetImageBytes();
            using (var ms = new MemoryStream(buffer))
            {
                request.InputStream = ms;
                var task = client.PutObjectAsync(request);
                var res = task.Result;
            }
        }
    }
}
