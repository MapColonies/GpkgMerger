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

        public S3(string serviceUrl, string bucket, string path, int batchSize) : base(DataType.S3, path, batchSize, new S3Utils(S3.GetClient(serviceUrl), bucket, path))
        {
            this.bucket = bucket;
            this.continuationToken = null;
            this.endOfRead = false;
            this.client = S3.GetClient(serviceUrl);
        }

        public S3(AmazonS3Client client, string bucket, string path, int batchSize) : base(DataType.S3, path, batchSize, new S3Utils(client, bucket, path))
        {
            this.bucket = bucket;
            this.continuationToken = null;
            this.endOfRead = false;
            this.client = client;
        }

        public static AmazonS3Client GetClient(string serviceUrl)
        {
            // Get s3 credentials
            string accessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY");
            string secretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY");
            var credentials = new BasicAWSCredentials(accessKey, secretKey);

            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.USEast1,
                ServiceURL = serviceUrl,
                ForcePathStyle = true
            };

            return new AmazonS3Client(credentials, config);
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
                    Coord coord = PathUtils.FromPath(item.Key);
                    coord.ToTms();
                    Tile tile = this.utils.GetTile(coord);
                    tiles.Add(tile);
                }
                this.continuationToken = response.NextContinuationToken;
            }

            this.endOfRead = !response.IsTruncated;

            return tiles;
        }

        public override void UpdateTiles(List<Tile> tiles)
        {
            foreach (var tile in tiles)
            {
                S3Utils.UpdateTile(this.client, this.bucket, this.path, tile);
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

        public override int TileCount()
        {
            int tileCount = 0;
            string continuationToken = null;

            do
            {
                var listRequests = new ListObjectsV2Request
                {
                    BucketName = this.bucket,
                    Prefix = this.path,
                    StartAfter = this.path,
                    ContinuationToken = continuationToken
                };

                var task = this.client.ListObjectsV2Async(listRequests);
                var response = task.Result;

                tileCount += response.KeyCount;
                continuationToken = response.NextContinuationToken;
            } while (continuationToken != null);

            return tileCount;
        }
    }
}
