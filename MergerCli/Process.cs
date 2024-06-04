using MergerCli.Utils;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using static MergerLogic.ImageProcessing.TileFormatStrategy;

namespace MergerCli
{
    internal class Process : IProcess
    {
        private readonly IConfigurationManager _configManager;
        private readonly ITileMerger _tileMerger;
        private readonly ILogger _logger;
        private TileFormatStrategy _tileFormatStrategy;
        static readonly object _locker = new object();

        public Process(IConfigurationManager configuration, ITileMerger tileMerger, ILogger<Process> logger)
        {
            this._configManager = configuration;
            this._tileMerger = tileMerger;
            this._logger = logger;

            FormatStrategy outputFormatStrategy = this._configManager.GetConfiguration<FormatStrategy>("TILE", "outputFormatStrategy");
            TileFormat outputFormat = this._configManager.GetConfiguration<TileFormat>("TILE", "outputFormat");
            this._tileFormatStrategy = new TileFormatStrategy(outputFormat, outputFormatStrategy);
        }

        public void Start(IData baseData, IData newData, BatchStatusManager batchStatusManager)
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
                this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] Resume mode activated, resume batchId: {resumeBatchIdentifier}");

                // Set strategy from status manager
                this._tileFormatStrategy = new TileFormatStrategy(batchStatusManager.Format, batchStatusManager.Strategy);

                // fix resume progress bug for gpkg, fs and web, fixing it for s3 requires storing additional data.
                if (newData.Type != DataType.S3)
                {
                    long totalCompletedTiles = batchStatusManager.GetTotalCompletedTiles(newData.Path);
                    tileProgressCount = totalCompletedTiles;
                }
            }

            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] Total amount of tiles to merge: {totalTileCount - tileProgressCount}");
            
            ParallelRun(baseData, newData, batchStatusManager,
                tileProgressCount, totalTileCount, resumeBatchIdentifier, resumeMode, pollForBatch);
            
            batchStatusManager.CompleteLayer(newData.Path);
            newData.Reset();
            // base data wrap up is in program as the same base data object is used in multiple calls 
        }

        private (List<Tile> newTiles, string currentBatchIdentifier) ManageBatchIdentifier(BatchStatusManager batchStatusManager, IData newData, string? resumeBatchIdentifier, long totalTileCount, ref bool resumeMode)
        {
            lock (_locker)
            {
                if (resumeMode)
                {
                    var incompleteBatches = batchStatusManager.GetBatches(newData.Path);
                    if (incompleteBatches != null && incompleteBatches.All(batch => batch.Value == true))
                    {
                        resumeMode = false;

                        if (resumeBatchIdentifier != null)
                        {
                            newData.setBatchIdentifier(resumeBatchIdentifier);
                        }
                    }
                    
                    var incompleteBatch = batchStatusManager.GetFirstIncompleteBatch(newData.Path);
                    if (incompleteBatch is not null)
                    {
                        newData.setBatchIdentifier(incompleteBatch.Value.Key);
                    }
                }

                List<Tile> newTiles = newData.GetNextBatch(out string? currentBatchIdentifier, out string? nextBatchIdentifier, totalTileCount);
                
                if (!resumeMode && newTiles.Count != 0)
                {
                    batchStatusManager.AssignBatch(newData.Path, currentBatchIdentifier);
                    batchStatusManager.SetCurrentBatch(newData.Path, nextBatchIdentifier);
                }
                
                return (newTiles, currentBatchIdentifier);
            }
        }
        
        private void ProcessBatch(IData baseData, List<Tile> newTiles, ref long tileProgressCount, long totalTileCount,ref bool pollForBatch)
        {
            ConcurrentBag<Tile> tiles = new ConcurrentBag<Tile>();
            
            if (newTiles.Count == 0)
            {
                pollForBatch = false;
                return;
            }

            bool uploadOnly = this._configManager.GetConfiguration<bool>("GENERAL", "uploadOnly") || baseData.IsNew;
            uploadOnly = uploadOnly || baseData.IsNew;
            bool shouldUpscale = !uploadOnly;

            for (int i = 0; i < newTiles.Count; i++)
            {
                var newTile = newTiles[i];
                var targetCoords = newTile.GetCoord();
                List<CorrespondingTileBuilder> correspondingTileBuilders = new List<CorrespondingTileBuilder>()
                {
                    () => baseData.GetCorrespondingTile(targetCoords, shouldUpscale),
                    () => newTile
                };

                Tile? tile = this._tileMerger.MergeTiles(correspondingTileBuilders, targetCoords, this._tileFormatStrategy, uploadOnly);

                if (tile != null)
                {
                    tiles.Add(tile);
                }
            }

            baseData.UpdateTiles(tiles);

            Interlocked.Add(ref tileProgressCount, tiles.Count);
            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] Tile Count: {tileProgressCount} / {totalTileCount}");
        }

        private void ParallelRun(IData baseData, IData newData,
            BatchStatusManager batchStatusManager, long tileProgressCount, long totalTileCount, string? resumeBatchIdentifier, bool resumeMode,bool pollForBatch)
        {
            var numOfThreads = this._configManager.GetConfiguration<int>("GENERAL", "parallel", "numOfThreads");
            Parallel.For(0, numOfThreads, new ParallelOptions { MaxDegreeOfParallelism = -1 }, _ =>
            {
                while (tileProgressCount != totalTileCount && pollForBatch)
                {
                    var batchResult = ManageBatchIdentifier(batchStatusManager, newData, resumeBatchIdentifier, totalTileCount, ref resumeMode);
                    ProcessBatch(baseData, batchResult.newTiles, ref tileProgressCount,
                        totalTileCount, ref pollForBatch);
                    batchStatusManager.CompleteBatch(newData.Path, batchResult.currentBatchIdentifier, tileProgressCount);
                }
            });
        }
        
        public void Validate(IData baseData, IData newData, string? incompleteBatchIdentifier)
        {
            List<Tile> newTiles;
            bool hasSameTiles = true;

            long totalTileCount = newData.TileCount();
            long tilesChecked = 0;
            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] Base tile Count: {baseData.TileCount()}, New tile count: {totalTileCount}");

            do
            {
                newTiles = newData.GetNextBatch(out _, out _, totalTileCount);

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
                        this._logger.LogError($"[{MethodBase.GetCurrentMethod().Name}] Missing tile: {newTile}");
                    }
                }

                newTileCount += newTiles.Count;
                tilesChecked += newTiles.Count;
                this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] Total tiles checked: {tilesChecked}/{totalTileCount}");
                hasSameTiles = newTileCount == baseMatchCount;
            } while (hasSameTiles && newTiles.Count > 0);

            newData.Reset();

            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] Target's valid: {hasSameTiles}");
        }
    }
}
