using System;
using Amazon.S3;
using Amazon.Runtime;
using System.Collections.Generic;
using GpkgMerger.Src.Batching;
using Amazon.S3.Model;
using System.IO;
using GpkgMerger.Src.Utils;

namespace GpkgMerger.Src.DataTypes
{
    public class S3 : Data
    {
        private AmazonS3Client client;
        private string bucket;


        ListObjectsV2Response listResponse;
        private string continuationToken;

        private bool endOfRead;

        public S3(string serviceUrl, string bucket, string path) : base(DataType.S3, path)
        {
            this.bucket = bucket;
            this.listResponse = null;
            this.continuationToken = null;
            this.endOfRead = false;

            // Get s3 credentials
            string accessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY");
            string secretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY");
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.USEast1,
                ServiceURL = serviceUrl,
                UseHttp = true,
                ForcePathStyle = true
            };
            client = new AmazonS3Client(credentials, config);
        }

        public override void Cleanup()
        {
            Console.WriteLine("S3 source, skipping cleanup phase");
        }

        private string GetImageHex(string key)
        {
            try
            {
                var getObjectTask = this.client.GetObjectAsync(this.bucket, key);
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
                Console.WriteLine($"Error: {e.Message}");
                return null;
            }
        }

        private void UploadTile(Tile tile)
        {
            string key = $"{this.path}{tile.Z}/{tile.X}/{tile.Y}.png";

            var request = new PutObjectRequest()
            {
                BucketName = this.bucket,
                CannedACL = S3CannedACL.PublicRead,
                Key = String.Format(key)
            };

            byte[] buffer = StringUtils.StringToByteArray(tile.Blob);
            using (var ms = new MemoryStream(buffer))
            {
                request.InputStream = ms;
                var task = this.client.PutObjectAsync(request);
                var res = task.Result;
            }
        }

        public override List<Tile> GetCorrespondingBatch(List<Tile> tiles)
        {
            List<Tile> correspondingTiles = new List<Tile>();

            foreach (Tile tile in tiles)
            {
                string key = $"{this.path}{tile.Z}/{tile.X}/{tile.Y}.png";

                string blob = GetImageHex(key);
                if (blob != null)
                {
                    Tile correspondingTile = new Tile(tile.Z, tile.X, tile.Y, blob, blob.Length);
                    correspondingTiles.Add(correspondingTile);
                }
                else
                {
                    correspondingTiles.Add(null);
                }

            }

            return correspondingTiles;
        }

        public override List<Tile> GetNextBatch()
        {
            List<Tile> tiles = new List<Tile>();

            var listRequests = new ListObjectsV2Request
            {
                BucketName = this.bucket,
                Prefix = this.path,
                StartAfter = this.path,
                MaxKeys = 5,
                ContinuationToken = this.continuationToken
            };

            var listObjectsTask = this.client.ListObjectsV2Async(listRequests);
            var response = listObjectsTask.Result;

            if (!endOfRead)
            {
                response.ContinuationToken = this.continuationToken;
                foreach (S3Object item in response.S3Objects)
                {
                    string key = item.Key;
                    string subPath = item.Key.Remove(0, this.path.Length);
                    string[] parts = subPath.Split('/');


                    string blob = GetImageHex(key);
                    string[] last = parts[2].Split('.');
                    int z = int.Parse(parts[0]);
                    int x = int.Parse(parts[1]);
                    int y = int.Parse(last[0]);

                    Tile tile = new Tile(z, x, y, blob, blob.Length);
                    tiles.Add(tile);
                }
                this.continuationToken = response.NextContinuationToken;
            }

            this.endOfRead = !response.IsTruncated;

            return tiles;
        }

        public override void UpdateMetadata(Data data)
        {
            Console.WriteLine("S3 source, skipping metadata update");
        }

        public override void UpdateTiles(List<Tile> tiles)
        {
            foreach (var tile in tiles)
            {
                UploadTile(tile);
            }
        }

        public override bool Exists()
        {
            var listRequests = new ListObjectsV2Request
            {
                BucketName = this.bucket,
                Prefix = this.path,
                StartAfter = this.path,
                MaxKeys = 1
            };

            Console.WriteLine($"Checking if exists, bucket: {this.bucket}, path: {this.path}");
            var task = this.client.ListObjectsV2Async(listRequests);

            var response = task.Result;
            return response.KeyCount > 0;
        }
    }
}