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

        private string GetImageHex(string key)
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

                return StringUtils.ByteArrayToString(image);
            }
            catch (AggregateException e)
            {
                // Console.WriteLine($"Error getting tile (key={key}): {e.Message}");
                return null;
            }
        }

        public override Tile GetTile(int z, int x, int y)
        {
            // Convert to TMS
            y = GeoUtils.FlipY(z, y);
            string key = PathUtils.GetTilePath(this.path, z, x, y, true);

            string blob = this.GetImageHex(key);
            if (blob == null)
            {
                return null;
            }
            // Convert from TMS
            y = GeoUtils.FlipY(z, y);
            return new Tile(z, x, y, blob, blob.Length);
        }

        public static void UpdateTile(AmazonS3Client client, string bucket, string path, Tile tile)
        {
            int y = GeoUtils.FlipY(tile);
            string key = PathUtils.GetTilePath(path, tile.Z, tile.X, y, true);

            var request = new PutObjectRequest()
            {
                BucketName = bucket,
                CannedACL = S3CannedACL.PublicRead,
                Key = String.Format(key)
            };

            byte[] buffer = StringUtils.StringToByteArray(tile.Blob);
            using (var ms = new MemoryStream(buffer))
            {
                request.InputStream = ms;
                var task = client.PutObjectAsync(request);
                var res = task.Result;
            }
        }
    }
}
