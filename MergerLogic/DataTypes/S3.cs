using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
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
            continuationToken = null;
            endOfRead = false;
            client = S3.GetClient(serviceUrl);
        }

        public S3(AmazonS3Client client, string bucket, string path, int batchSize) : base(DataType.S3, path, batchSize, new S3Utils(client, bucket, path))
        {
            this.bucket = bucket;
            continuationToken = null;
            endOfRead = false;
            this.client = client;
        }

        ~S3()
        {
            client.Dispose();
        }

        public override void Reset()
        {
            continuationToken = null;
            endOfRead = false;
        }

        public override void Wrapup()
        {
            base.Wrapup();
            Reset();
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
                BucketName = bucket,
                Prefix = path,
                StartAfter = path,
                MaxKeys = batchSize,
                ContinuationToken = continuationToken
            };

            var listObjectsTask = client.ListObjectsV2Async(listRequests);
            var response = listObjectsTask.Result;

            if (!endOfRead)
            {
                response.ContinuationToken = continuationToken;
                foreach (S3Object item in response.S3Objects)
                {
                    string key = item.Key;
                    Coord coord = PathUtils.FromPath(item.Key, true);
                    coord.flipY();
                    Tile tile = utils.GetTile(coord);
                    tiles.Add(tile);
                }
                continuationToken = response.NextContinuationToken;
            }

            endOfRead = !response.IsTruncated;

            return tiles;
        }

        public override void UpdateTiles(List<Tile> tiles)
        {
            foreach (var tile in tiles)
            {
                S3Utils.UpdateTile(client, bucket, path, tile);
            }
        }

        public override bool Exists()
        {
            var listRequests = new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = path,
                StartAfter = path,
                MaxKeys = 1
            };

            Console.WriteLine($"Checking if exists, bucket: {bucket}, path: {path}");
            var task = client.ListObjectsV2Async(listRequests);
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
                    BucketName = bucket,
                    Prefix = path,
                    StartAfter = path,
                    ContinuationToken = continuationToken
                };

                var task = client.ListObjectsV2Async(listRequests);
                var response = task.Result;

                tileCount += response.KeyCount;
                continuationToken = response.NextContinuationToken;
            } while (continuationToken != null);

            return tileCount;
        }
    }
}
