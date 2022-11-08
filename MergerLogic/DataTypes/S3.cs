using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MergerLogic.DataTypes
{
    public class S3 : Data<IS3Client>
    {
        private IAmazonS3 _client;
        private string _bucket;
        private readonly List<int> _zoomLevels;
        private IEnumerator<int> _zoomEnumerator;
        private string? _continuationToken;
        private bool _endOfRead;

        private readonly IPathUtils _pathUtils;

        public S3(IPathUtils pathUtils, IServiceProvider container,
            string path, int batchSize, Grid? grid, GridOrigin? origin, bool isBase)
            : base(container, DataType.S3, path, batchSize, grid, origin, isBase)
        {
            this._pathUtils = pathUtils;
            this._continuationToken = null;
            this._endOfRead = false;

            // This should always happen after the definition of the client
            this._zoomLevels = this.GetZoomLevels();
            this._zoomEnumerator = this._zoomLevels.GetEnumerator();
            // In order to get a correct first value we must do an initial MoveNext call
            this._zoomEnumerator.MoveNext();
        }

        protected override void Initialize()
        {
            var configurationManager = this._container.GetRequiredService<IConfigurationManager>();
            var client = this._container.GetService<IAmazonS3>();
            this._client = client ?? throw new Exception("s3 configuration is required");
            this._bucket = configurationManager.GetConfiguration("S3", "bucket");
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.LOWER_LEFT;
        }

        public override void Reset()
        {
            this._continuationToken = null;
            this._endOfRead = false;
            this._zoomEnumerator = this._zoomLevels.GetEnumerator();
            // In order to get a correct first value we must do an initial MoveNext call
            this._zoomEnumerator.MoveNext();
        }

        private List<int> GetZoomLevels()
        {
            List<int> zoomLevels = new List<int>();
            
            for (int zoomLevel = 0; zoomLevel < Data<IS3Client>.MaxZoomRead; zoomLevel++)
            {
                if (this.FolderExists($"{zoomLevel}/"))
                {
                    zoomLevels.Add(zoomLevel);
                }
            }

            return zoomLevels;
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            batchIdentifier = this._continuationToken;
            List<Tile> tiles = new List<Tile>();
            int missingTiles = this.BatchSize;

            while (missingTiles > 0)
            {
                if (this._endOfRead && !this._zoomEnumerator.MoveNext())
                {
                    break;
                }

                string path = $"{this.Path}/{this._zoomEnumerator.Current}/";

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
