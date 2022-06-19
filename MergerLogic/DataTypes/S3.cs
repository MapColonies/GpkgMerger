using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MergerLogic.DataTypes
{
    public class S3 : Data<IS3Utils>
    {
        private readonly ILogger _logger;
        private readonly IAmazonS3 _client;
        private readonly string _bucket;
        private string? _continuationToken;
        private bool _endOfRead;

        private readonly IPathUtils _pathUtils;

        public S3(IPathUtils pathUtils, IAmazonS3 client, IServiceProvider container,
            string bucket, string path, int batchSize, bool isOneXOne = false, GridOrigin origin = GridOrigin.LOWER_LEFT)
            : base(container, DataType.S3, path, batchSize, isOneXOne, origin)
        {
            this._bucket = bucket;
            this._continuationToken = null;
            this._endOfRead = false;
            this._client = client;
            this._pathUtils = pathUtils;
            this._logger = container.GetService<ILogger<S3>>();
        }

        ~S3()
        {
            this._client.Dispose();
        }

        public override void Reset()
        {
            this._continuationToken = null;
            this._endOfRead = false;
        }

        public override void Wrapup()
        {
            base.Wrapup();
            this.Reset();
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            batchIdentifier = this._continuationToken;
            List<Tile> tiles = new List<Tile>();

            var listRequests = new ListObjectsV2Request
            {
                BucketName = this._bucket,
                Prefix = this.Path,
                StartAfter = this.Path,
                MaxKeys = this.BatchSize,
                ContinuationToken = this._continuationToken
            };

            var listObjectsTask = this._client.ListObjectsV2Async(listRequests);
            var response = listObjectsTask.Result;

            if (!this._endOfRead)
            {
                response.ContinuationToken = this._continuationToken;
                foreach (S3Object item in response.S3Objects)
                {
                    Coord coords = this._pathUtils.FromPath(item.Key, true);
                    Tile tile = this.Utils.GetTile(coords);
                    if (tile != null)
                    {
                        tile = this._toCurrentGrid(tile);
                        if (tile != null)
                        {
                            tile = this._convertOriginTile(tile);
                            tiles.Add(tile);
                        }
                    }
                }
                this._continuationToken = response.NextContinuationToken;
            }

            this._endOfRead = !response.IsTruncated;

            return tiles;
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this._continuationToken = batchIdentifier;
        }

        public override bool Exists()
        {
            var listRequests = new ListObjectsV2Request
            {
                BucketName = this._bucket,
                Prefix = this.Path,
                StartAfter = this.Path,
                MaxKeys = 1
            };

            this._logger.LogInformation($"Checking if exists, bucket: {this._bucket}, path: {this.Path}");
            var task = this._client.ListObjectsV2Async(listRequests);
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
                    BucketName = this._bucket,
                    Prefix = this.Path,
                    StartAfter = this.Path,
                    ContinuationToken = continuationToken
                };

                var task = this._client.ListObjectsV2Async(listRequests);
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
                this.Utils.UpdateTile(tile);
            }
        }
    }
}
