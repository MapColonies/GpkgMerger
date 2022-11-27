using MergerCli.Utils;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace MergerCli
{
    internal class Process : IProcess
    {
        private Func<Coord, Tile?> _getTileByCoord;

        private readonly ITileMerger _tileMerger;
        private readonly ILogger _logger;
        static readonly object _locker = new object();
        public Process(ITileMerger tileMerger, ILogger<Process> logger)
        {
            this._tileMerger = tileMerger;
            this._logger = logger;
        }
        
        public void Start(TileFormat targetFormat, IData baseData, IData newData, int batchSize,
            BatchStatusManager batchStatusManager)
        {
            batchStatusManager.InitializeLayer(newData.Path);
            // ConcurrentBag<Tile> tiles = new ConcurrentBag<Tile>();
            long totalTileCount = newData.TileCount();
            long tileProgressCount = 0;
            bool resumeMode = false;

            string? resumeBatchIdentifier = batchStatusManager.GetLayerBatchIdentifier(newData.Path);
            if (resumeBatchIdentifier != null)
            {
                resumeMode = true;
                //handleResumeBatch(newData, batchStatusManager);
                // fix resume progress bug for gpkg, fs and web, fixing it for s3 requires storing additional data.
                if (newData.Type != DataType.S3)
                {
                    tileProgressCount = int.Parse(resumeBatchIdentifier);
                }
            }
            Console.WriteLine($"tileProgressCount: {tileProgressCount}");

            this._logger.LogInformation($"Total amount of tiles to merge: {totalTileCount - tileProgressCount}");

            _getTileByCoord = baseData.IsNew ?
                (_) => null
                :
                (targetCoords) => baseData.GetCorrespondingTile(targetCoords, true);
            List<string> threads = new List<string>(4);
            threads.Add("0");
            threads.Add("0");
            threads.Add("0");
            // threads.Add("0");
            // threads.Add("0");
            // threads.Add("0");


            //
            Parallel.ForEach(threads, b =>
            {
                do
                {
                    DoWork(targetFormat, baseData, newData, batchSize, batchStatusManager, ref tileProgressCount,
                        totalTileCount, resumeMode, resumeBatchIdentifier);
                } while (tileProgressCount != totalTileCount);
            });

            batchStatusManager.CompleteLayer(newData.Path);
            newData.Reset();
            // base data wrap up is in program as the same base data object is used in multiple calls 
        }

        private void DoWork(TileFormat targetFormat, IData baseData, IData newData, int batchSize,
            BatchStatusManager batchStatusManager, ref long tileProgressCount, long totalTileCount, bool resumeMode, string? resumeBatchIdentifier = null)
        {
            // if (resumeMode && resumeBatchIdentifier != null)
            // {
            //     newData.setBatchIdentifier(resumeBatchIdentifier);  
            // }
            Console.WriteLine($"tileProgressCount: {tileProgressCount}");
            List<Tile> newTiles = newData.GetNextBatch(out string batchIdentifier);
            Console.WriteLine($"Batch identifier: {batchIdentifier}");
            batchStatusManager.AssignBatch(newData.Path, batchIdentifier);
            ConcurrentBag<Tile> tiles = new ConcurrentBag<Tile>();
            if (resumeMode)
            {
                batchStatusManager.SetCurrentBatch(newData.Path, batchIdentifier);
            }
            
            tiles.Clear();
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
                    //Console.WriteLine($"Tiles Count: {tiles.Count}, Thread: {Thread.CurrentThread.ManagedThreadId}");

                }
                //Console.WriteLine($"Tiles count: {tiles.Count}");
            }

            baseData.UpdateTiles(tiles);
            if (tiles.Count != 0)
            {
                Interlocked.Add(ref tileProgressCount, tiles.Count);
                this._logger.LogInformation($"Tile Count: {tileProgressCount} / {totalTileCount}");
                batchStatusManager.CompleteBatch(newData.Path, batchIdentifier);
                

            }
            //Interlocked.Add(ref tileProgressCount, tiles.Count);
            //tileProgressCount += tiles.Count;
        }

        
        public void Validate(IData baseData, IData newData)
        {
            List<Tile> newTiles;
            bool hasSameTiles = true;

            long totalTileCount = newData.TileCount();
            long tilesChecked = 0;
            this._logger.LogInformation($"Base tile Count: {baseData.TileCount()}, New tile count: {totalTileCount}");

            do
            {
                newTiles = newData.GetNextBatch(out _);

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
        
        public void handleResumeBatch(IData newData, BatchStatusManager batchStatusManager)
        {
            ConcurrentDictionary<string, bool>? inCompletedBatches = batchStatusManager.GetBatches(newData.Path);
            if (!inCompletedBatches.IsEmpty)
            {
                do
                {
                    var inCompletedBatch = batchStatusManager.GetFirstInCompletedBatch(newData.Path);
                    Console.WriteLine($"incompleted batch: {inCompletedBatch}");
                    newData.setBatchIdentifier(inCompletedBatch.Value.Key);
                    
                    
                } while (batchStatusManager.GetBatches(newData.Path).IsEmpty);
            }


            //DoWork();
        }
    }
}
