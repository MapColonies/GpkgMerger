using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class S3 : Data<IS3Utils>
    {
        private IAmazonS3 client;
        private string bucket;
        private string? continuationToken;
        private bool endOfRead;

        private IPathUtils _pathUtils;

        public S3(IPathUtils pathUtils, IAmazonS3 client, IUtilsFactory utilsFactory, IOneXOneConvetor oneXOneConvetor,
            string bucket, string path, int batchSize, bool isOneXOne = false, GridOrigin origin = GridOrigin.LOWER_LEFT)
            : base(utilsFactory, oneXOneConvetor, DataType.S3, path, batchSize, isOneXOne, origin)
        {
            this.bucket = bucket;
            this.continuationToken = null;
            this.endOfRead = false;
            this.client = client;
            this._pathUtils = pathUtils;
        }

        ~S3()
        {
            this.client.Dispose();
        }

        public override void Reset()
        {
            this.continuationToken = null;
            this.endOfRead = false;
        }

        public override void Wrapup()
        {
            base.Wrapup();
            this.Reset();
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            batchIdentifier = this.continuationToken;
            List<Tile> tiles = new List<Tile>();

            var listRequests = new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = Path,
                StartAfter = Path,
                MaxKeys = batchSize,
                ContinuationToken = continuationToken
            };

            var listObjectsTask = this.client.ListObjectsV2Async(listRequests);
            var response = listObjectsTask.Result;

            if (!this.endOfRead)
            {
                response.ContinuationToken = this.continuationToken;
                foreach (S3Object item in response.S3Objects)
                {
                    Coord coord = this._pathUtils.FromPath(item.Key, true);
                    Tile tile = this.utils.GetTile(coord);
                    tile = this._toCurrentGrid(tile);
                    if (tile != null)
                    {
                        tile = this._convertOriginTile(tile);
                        tiles.Add(tile);
                    }
                }
                this.continuationToken = response.NextContinuationToken;
            }

            this.endOfRead = !response.IsTruncated;

            return tiles;
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this.continuationToken = batchIdentifier;
        }

        public override bool Exists()
        {
            var listRequests = new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = Path,
                StartAfter = Path,
                MaxKeys = 1
            };

            Console.WriteLine($"Checking if exists, bucket: {this.bucket}, path: {this.Path}");
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
                    BucketName = bucket,
                    Prefix = Path,
                    StartAfter = Path,
                    ContinuationToken = continuationToken
                };

                var task = this.client.ListObjectsV2Async(listRequests);
                var response = task.Result;

                tileCount += response.KeyCount;
                continuationToken = response.NextContinuationToken;
            } while (continuationToken != null);

            return tileCount;
        }

        protected override void InternalUpdateTiles(IEnumerable<Tile> targetTiles)
        {
            foreach (var tile in targetTiles)
            {
                this.utils.UpdateTile(tile);
            }
        }
    }
}
