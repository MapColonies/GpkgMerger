using System;
using Amazon.S3;
using Amazon.Runtime;
using System.Collections.Generic;
using GpkgMerger.Src.Batching;
using Amazon.S3.Model;
using GpkgMerger.Src.Utils;

namespace GpkgMerger.Src.DataTypes
{
    public class S3 : Data
    {
        private AmazonS3Client client;
        private string bucket;
        private string continuationToken;
        private bool endOfRead;

        public S3(string serviceUrl, string bucket, string path, int batchSize) : base(DataType.S3, path, batchSize)
        {
            this.bucket = bucket;
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

        public override List<Tile> GetCorrespondingBatch(List<Tile> tiles)
        {
            List<Tile> correspondingTiles = new List<Tile>();

            foreach (Tile tile in tiles)
            {
                string key = $"{this.path}{tile.Z}/{tile.X}/{tile.Y}.png";

                string blob = S3Utils.GetImageHex(this.client, this.bucket, key);
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
                MaxKeys = this.batchSize,
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


                    string blob = S3Utils.GetImageHex(this.client, this.bucket, key);
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
                S3Utils.UploadTile(this.client, this.bucket, this.path, tile);
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