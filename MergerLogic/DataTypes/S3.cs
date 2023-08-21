using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.Monitoring.Metrics;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

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
        static readonly object _locker = new object();
        private const string nullStringValue = "Null";

        private readonly IPathUtils _pathUtils;

        public S3(IPathUtils pathUtils, IServiceProvider container, IMetricsProvider metricsProvider,
            string path, int batchSize, Grid? grid, GridOrigin? origin, bool isBase)
            : base(container, metricsProvider, DataType.S3, path, batchSize, grid, origin, isBase)
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] Ctor started");
            this._pathUtils = pathUtils;
            this._continuationToken = null;
            this._endOfRead = false;

            // This should always happen after the definition of the client
            this._zoomLevels = this.GetZoomLevels();
            this._zoomEnumerator = this._zoomLevels.GetEnumerator();
            // In order to get a correct first value we must do an initial MoveNext call
            this._zoomEnumerator.MoveNext();
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] Ctor ended");
        }

        protected override void Initialize()
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] start");
            var configurationManager = this._container.GetRequiredService<IConfigurationManager>();
            var client = this._container.GetService<IAmazonS3>();
            this._client = client ?? throw new Exception("s3 configuration is required");
            this._bucket = configurationManager.GetConfiguration("S3", "bucket");
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] ended");
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.LOWER_LEFT;
        }

        public override void Reset()
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] start");
            this._continuationToken = null;
            this._endOfRead = false;
            this._zoomEnumerator = this._zoomLevels.GetEnumerator();
            // In order to get a correct first value we must do an initial MoveNext call
            this._zoomEnumerator.MoveNext();
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] ended");
        }

        private List<int> GetZoomLevels()
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] start");
            List<int> zoomLevels = new List<int>();
            
            for (int zoomLevel = 0; zoomLevel < Data<IS3Client>.MaxZoomRead; zoomLevel++)
            {
                if (this.FolderExists($"{zoomLevel}/"))
                {
                    zoomLevels.Add(zoomLevel);
                }
            }
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] ended");
            return zoomLevels;
        }

        public override List<Tile> GetNextBatch(out string currentBatchIdentifier,out string? nextBatchIdentifier, long? totalTilesCount)
        {
            lock (_locker)
            {
                this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] start");
                currentBatchIdentifier = this._continuationToken ?? nullStringValue;
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
                
                nextBatchIdentifier = this._continuationToken;
                this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] end");
                return tiles;
            }
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this._continuationToken = batchIdentifier;
        }

        private bool FolderExists(string directory)
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] start");
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
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] end");
            return response.KeyCount > 0;
        }

        public override bool Exists()
        {
            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] bucket: {this._bucket}, path: {this.Path}");
            bool exists = FolderExists("");
            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] ended");
            return exists;
        }

        public override long TileCount()
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] start");
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
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] end");
            return tileCount;
        }

        protected override void InternalUpdateTiles(IEnumerable<Tile> targetTiles)
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] start");
            foreach (var tile in targetTiles)
            {
                this.Utils.UpdateTile(tile);
            }
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] end");
        }
    }
}
