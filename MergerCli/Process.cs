using MergerCli.Utils;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MergerCli
{
    internal class Process : IProcess
    {
        private Func<Coord, Tile?> _getTileByCoord;
        private readonly IConfigurationManager _configManager;
        private readonly ITileMerger _tileMerger;
        private readonly ILogger _logger;

        public Process(IConfigurationManager configuration, ITileMerger tileMerger, ILogger<Process> logger)
        {
            this._configManager = configuration;
            this._tileMerger = tileMerger;
            this._logger = logger;
        }

        public void Start(TileFormat targetFormat, IData baseData, IData newData, int batchSize,
            BatchStatusManager batchStatusManager)
        {
            long totalTileCount = newData.TileCount();
            batchStatusManager.InitializeLayer(newData.Path);
            long tileProgressCount = 0;
            bool resumeMode = false;
            bool pollForBatch = true;

            string? resumeBatchIdentifier = batchStatusManager.GetLayerBatchIdentifier(newData.Path);
            if (resumeBatchIdentifier != null)
            {
                resumeMode = true;
                this._logger.LogDebug($"Resume mode activated");
                // fix resume progress bug for gpkg, fs and web, fixing it for s3 requires storing additional data.
                if (newData.Type != DataType.S3)
                {
                    long totalCompletedTiles = batchStatusManager.GetTotalCompletedTiles(newData.Path);
                    tileProgressCount = totalCompletedTiles;
                }
            }

            this._logger.LogInformation($"Total amount of tiles to merge: {totalTileCount - tileProgressCount}");
            var uploadOnly = this._configManager.GetConfiguration<bool>("GENERAL", "uploadOnly");
            _getTileByCoord = uploadOnly || baseData.IsNew ?
                (_) => null
                :
                (targetCoords) => baseData.GetCorrespondingTile(targetCoords, true);

            ParallelRun(targetFormat, baseData, newData, batchStatusManager,
                tileProgressCount, totalTileCount, resumeBatchIdentifier, resumeMode, pollForBatch);
            
            batchStatusManager.CompleteLayer(newData.Path);
            newData.Reset();
            // base data wrap up is in program as the same base data object is used in multiple calls 
        }

        private void DoWork(TileFormat targetFormat, IData baseData, IData newData,
            BatchStatusManager batchStatusManager, ref long tileProgressCount, long totalTileCount, bool resumeMode,ref bool pollForBatch, string? incompleteBatchIdentifier = null)
        {
            ConcurrentBag<Tile> tiles = new ConcurrentBag<Tile>();
            List<Tile> newTiles = newData.GetNextBatch(out string currentBatchIdentifier, out string? nextBatchIdentifier, incompleteBatchIdentifier, totalTileCount);
            if (!resumeMode && newTiles.Count > 0)
            {
                batchStatusManager.AssignBatch(newData.Path, currentBatchIdentifier);
                batchStatusManager.SetCurrentBatch(newData.Path, nextBatchIdentifier);
            }

            if (newTiles.Count > 0)
            {
                for (int i = 0; i < newTiles.Count; i++)
                {
                    var newTile = newTiles[i];
                    var targetCoords = newTile.GetCoord();
                    List<CorrespondingTileBuilder> correspondingTileBuilders = new List<CorrespondingTileBuilder>()
                    {
                        () => _getTileByCoord(targetCoords),
                        () => newTile
                    };

                    byte[]? image = this._tileMerger.MergeTiles(correspondingTileBuilders, targetCoords, targetFormat);

                    if (image != null)
                    {
                        newTile = new Tile(newTile.Z, newTile.X, newTile.Y, image);
                        tiles.Add(newTile);
                    }
                }
            }

            baseData.UpdateTiles(tiles);
            if (tiles.Count != 0)
            {
                Interlocked.Add(ref tileProgressCount, tiles.Count);
                this._logger.LogInformation($"Tile Count: {tileProgressCount} / {totalTileCount}");
                batchStatusManager.CompleteBatch(newData.Path, currentBatchIdentifier, tileProgressCount);
            }
            else
            {
                pollForBatch = false;
            }
        }

        private void ParallelRun(TileFormat targetFormat, IData baseData, IData newData,
            BatchStatusManager batchStatusManager, long tileProgressCount, long totalTileCount, string? resumeBatchIdentifier, bool resumeMode,bool pollForBatch)
        {
            var numOfThreads = this._configManager.GetConfiguration<int>("GENERAL", "parallel", "numOfThreads");
            // use max of available cpus to the proccess
            Parallel.For(0, numOfThreads, new ParallelOptions { MaxDegreeOfParallelism = -1 }, _ =>
            {
                while (tileProgressCount != totalTileCount && pollForBatch)
                {
                    if (resumeMode)
                    {
                        var incompleteBatches = batchStatusManager.GetBatches(newData.Path);
                        while (incompleteBatches is { IsEmpty: false })
                        {
                            var incompleteBatch = batchStatusManager.GetFirstIncompleteBatch(newData.Path);
                            if (incompleteBatch is not null)
                            {
                                DoWork(targetFormat, baseData, newData, batchStatusManager,
                                    ref tileProgressCount,
                                    totalTileCount, resumeMode,ref pollForBatch, incompleteBatch.Value.Key);
                            }
                        }
            
                        resumeMode = false;
                        newData.setBatchIdentifier(resumeBatchIdentifier);
                    }
                    
                    if (tileProgressCount != totalTileCount)
                    {
                        DoWork(targetFormat, baseData, newData, batchStatusManager, ref tileProgressCount,
                            totalTileCount, resumeMode, ref pollForBatch);
                    }
                }
            });
        }
        public void Validate(IData baseData, IData newData, string? incompleteBatchIdentifier)
        {
            List<Tile> newTiles;
            bool hasSameTiles = true;

            long totalTileCount = newData.TileCount();
            long tilesChecked = 0;
            this._logger.LogInformation($"Base tile Count: {baseData.TileCount()}, New tile count: {totalTileCount}");

            do
            {
                newTiles = newData.GetNextBatch(out _, out _, incompleteBatchIdentifier, totalTileCount);

                int baseMatchCount = 0;
                int newTileCount = 0;
                for (int i = 0; i < newTiles.Count; i++)
                {
                    Tile newTile = newTiles[i];
                    bool baseTileExists = baseData.TileExists(newTile.GetCoord());

                    if (baseTileExists)
                    {
                        ++baseMatchCount;
                    }
                    else
                    {
                        this._logger.LogError($"Missing tile: {newTile}");
                    }
                }

                newTileCount += newTiles.Count;
                tilesChecked += newTiles.Count;
                this._logger.LogInformation($"Total tiles checked: {tilesChecked}/{totalTileCount}");
                hasSameTiles = newTileCount == baseMatchCount;
            } while (hasSameTiles && newTiles.Count > 0);

            newData.Reset();

            this._logger.LogInformation($"Target's valid: {hasSameTiles}");
        }
    }
}
