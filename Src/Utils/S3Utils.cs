using System;
using System.IO;
using System.Linq;
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
                var getObjectTask = client.GetObjectAsync(bucket, key);
                GetObjectResponse res = getObjectTask.Result;

                byte[] image;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (var responseStream = res.ResponseStream)
                    {
                        var buffer = new byte[1000];
                        var bytesRead = 0;
                        while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, bytesRead);
                        }
                    }
                    image = ms.ToArray();
                }

                return Convert.ToHexString(image);
            }
            catch (AggregateException e)
            {
                Console.WriteLine($"Error getting tile (key={key}): {e.Message}");
                return null;
            }
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