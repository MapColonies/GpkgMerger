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
                ForcePathStyle = true
            };
            client = new AmazonS3Client(credentials, config);
        }

        private Coord FromKey(string key)
        {
            string[] parts = key.Split('/');
            int numParts = parts.Length;

            // Each key represents a tile, therfore the last three parts represent the z, x and y values
            string[] last = parts[numParts - 1].Split('.');
            int z = int.Parse(parts[numParts - 3]);
            int x = int.Parse(parts[numParts - 2]);
            int y = int.Parse(last[0]);

            return new Coord(z, x, y);
        }

        public override void Cleanup()
        {
            Console.WriteLine("S3 source, skipping cleanup phase");
        }

        protected override List<Tile> CreateCorrespondingBatch(List<Tile> tiles, bool upscale)
        {
            List<Tile> correspondingTiles = new List<Tile>();

            foreach (Tile tile in tiles)
            {
                Tile correspondingTile = S3Utils.GetTile(this.client, this.bucket, this.path, tile.Z, tile.X, tile.Y);

                if (upscale && correspondingTile == null)
                {
                    correspondingTile = GetLastExistingTile(tile);
                }

                correspondingTiles.Add(correspondingTile);
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
                    Coord coord = FromKey(item.Key);
                    Tile tile = S3Utils.GetTileTMS(this.client, this.bucket, this.path, coord);
                    tiles.Add(tile);
                }
                this.continuationToken = response.NextContinuationToken;
            }

            this.endOfRead = !response.IsTruncated;

            return tiles;
        }

        protected override Tile GetLastExistingTile(Tile tile)
        {
            int z = tile.Z;
            int baseTileX = tile.X;
            int baseTileY = tile.Y;

            Tile lastTile = null;

            // Go over zoom levels until a tile is found (may not find tile)
            for (int i = z - 1; i >= 0; i--)
            {
                baseTileX >>= 1; // Divide by 2
                baseTileY >>= 1; // Divide by 2

                lastTile = S3Utils.GetTile(this.client, this.bucket, this.path, i, baseTileX, baseTileY);
                if (lastTile != null)
                {
                    break;
                }
            }

            return lastTile;
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
