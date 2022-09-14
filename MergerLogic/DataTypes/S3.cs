using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.Utils;
using Microsoft.Extensions.Logging;

namespace MergerLogic.DataTypes
{
    public class S3 : Data<IS3Client>
    {
        private readonly IAmazonS3 _client;
        private readonly string _bucket;
        private IEnumerator<int> _zoomLevels;
        private string? _continuationToken;
        private bool _endOfRead;

        private readonly IPathUtils _pathUtils;

        public S3(IPathUtils pathUtils, IAmazonS3 client, IServiceProvider container,
            string bucket, string path, int batchSize, Grid? grid, GridOrigin? origin)
            : base(container, DataType.S3, path, batchSize, grid, origin)
        {
            this._pathUtils = pathUtils;
            this._bucket = bucket;
            this._continuationToken = null;
            this._endOfRead = false;
            this._client = client;

            // This should always happen after the definition of the client
            this._zoomLevels = this.GetZoomLevels();
            this._zoomLevels.MoveNext();
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.LOWER_LEFT;
        }

        public override void Reset()
        {
            this._continuationToken = null;
            this._endOfRead = false;
            this._zoomLevels = this.GetZoomLevels();
            this._zoomLevels.MoveNext();
        }

        private IEnumerator<int> GetZoomLevels()
        {
            for (int zoomLevel = 0; zoomLevel < this.MaxZoomRead; zoomLevel++)
            {
                if (this.FolderExists($"{zoomLevel}/"))
                {
                    yield return zoomLevel;
                }
            }
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            batchIdentifier = this._continuationToken;
            List<Tile> tiles = new List<Tile>();
            int missingTiles = this.BatchSize;

            while (missingTiles > 0)
            {
                if (this._endOfRead && !this._zoomLevels.MoveNext())
                {
                    break;
                }

                string path = $"{this.Path}/{this._zoomLevels.Current}";
                
                var listRequests = new ListObjectsV2Request
                {
                    BucketName = this._bucket,
                    Prefix = path,
                    StartAfter = path,
                    MaxKeys = missingTiles,
                    ContinuationToken = this._continuationToken
                };

                var listObjectsTask = this._client.ListObjectsV2Async(listRequests);
                var response = listObjectsTask.Result;

                foreach (S3Object item in response.S3Objects)
                {
                    Tile? tile = this.Utils.GetTile(item.Key);
                    if (tile is null)
                    {
                        continue;
                    }

                    tile = this.ToCurrentGrid(tile);
                    if (tile is null)
                    {
                        continue;
                    }

                    tile = this.ConvertOriginTile(tile)!;
                    tiles.Add(tile);
                }

                missingTiles -= response.KeyCount;
                this._continuationToken = response.NextContinuationToken;
                this._endOfRead = !response.IsTruncated;
            }

            return tiles;
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this._continuationToken = batchIdentifier;
        }
        
        private bool FolderExists(string directory)
        {
            directory = $"{this.Path}/{directory}";
            
            var listRequests = new ListObjectsV2Request
            {
                BucketName = this._bucket,
                Prefix = directory,
                StartAfter = directory,
                MaxKeys = 1
            };

            var task = this._client.ListObjectsV2Async(listRequests);
            var response = task.Result;
            return response.KeyCount > 0;
        }

        public override bool Exists()
        {
            this._logger.LogInformation($"Checking if exists, bucket: {this._bucket}, path: {this.Path}");
            return FolderExists("");
        }

        public override long TileCount()
        {
            long tileCount = 0;
            string? continuationToken = null;

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
