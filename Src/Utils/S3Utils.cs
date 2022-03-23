using System;
using System.IO;
using Amazon.S3;
using Amazon.S3.Model;
using GpkgMerger.Src.Batching;

namespace GpkgMerger.Src.Utils
{
    public static class S3Utils
    {
        public static string GetImageHex(AmazonS3Client client, string bucket, string key)
        {
            try
            {
                var request = new GetObjectRequest()
                {
                    BucketName = bucket,
                    Key = key
                };
                var getObjectTask = client.GetObjectAsync(request);
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

        public static Tile GetTile(AmazonS3Client client, string bucket, string path, int z, int x, int y)
        {
            Tile correspondingTile = null;
            string key = $"{path}{z}/{z}/{y}.png";
            string blob = S3Utils.GetImageHex(client, bucket, key);
            if (blob != null)
            {
                correspondingTile = new Tile(z, x, y, blob, blob.Length);
            }
            return correspondingTile;
        }

        public static void UploadTile(AmazonS3Client client, string bucket, string path, Tile tile)
        {
            string key = $"{path}{tile.Z}/{tile.X}/{tile.Y}.png";

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