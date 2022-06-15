using MergerCli.Utils;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProccessing;

namespace MergerCli
{
    internal class Process : IProcess
    {
        private ITileMerger _tileMerger;
        public Process(ITileMerger tileMerger)
        {
            this._tileMerger = tileMerger;
        }

        public void Start(IData baseData, IData newData, int batchSize, BatchStatusManager batchStatusManager)
        {
            batchStatusManager.InitilaizeLayer(newData.Path);
            List<Tile> tiles = new List<Tile>(batchSize);
            int totalTileCount = newData.TileCount();
            int tileProgressCount = 0;

            string? resumeBatchIdentifier = batchStatusManager.GetLayerBatchIdentifier(newData.Path);
            if (resumeBatchIdentifier != null)
            {
                newData.setBatchIdentifier(resumeBatchIdentifier);
                // fix resume progress bug for gpkg, fs and web, fixing it for s3 requires storing additional data.
                if (newData.Type != DataType.S3)
                {
                    tileProgressCount = int.Parse(resumeBatchIdentifier);
                }
            }

            Console.WriteLine($"Total amount of tiles to merge: {totalTileCount}");

            // Update base metadata according to new data
            baseData.UpdateMetadata(newData);

            do
            {
                List<Tile> newTiles = newData.GetNextBatch(out string batchIdentifier);
                batchStatusManager.SetCurrentBatch(newData.Path, batchIdentifier);

                tiles.Clear();
                for (int i = 0; i < newTiles.Count; i++)
                {
                    var newTile = newTiles[i];
                    var targetCoords = newTile.GetCoord();
                    List<CorrespondingTileBuilder> correspondingTileBuilders = new List<CorrespondingTileBuilder>()
                    {
                        ()=>baseData.GetCorrespondingTile(targetCoords,true),
                        ()=> newTile
                    };

                    byte[]? image = this._tileMerger.MergeTiles(correspondingTileBuilders, targetCoords);

                    if (image != null)
                    {
                        newTile = new Tile(newTile.Z, newTile.X, newTile.Y, image);
                        tiles.Add(newTile);
                    }
                }

                baseData.UpdateTiles(tiles);

                tileProgressCount += tiles.Count;
                Console.WriteLine($"Tile Count: {tileProgressCount} / {totalTileCount}");

            } while (tiles.Count == batchSize);

            batchStatusManager.CompleteLayer(newData.Path);
            baseData.Wrapup();
            newData.Reset();
        }

        public void Validate(IData baseData, IData newData)
        {
            List<Tile> newTiles;
            bool hasSameTiles = true;

            int totalTileCount = newData.TileCount();
            int tilesChecked = 0;
            Console.WriteLine($"Base tile Count: {baseData.TileCount()}, New tile count: {newData.TileCount()}");

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
                        Console.WriteLine("Missing tiles:");
                        newTile.Print();
                    }
                }

                newTileCount += newTiles.Count;
                tilesChecked += newTiles.Count;
                Console.WriteLine($"Total tiles checked: {tilesChecked}/{totalTileCount}");
                hasSameTiles = newTileCount == baseMatchCount;

            } while (hasSameTiles && newTiles.Count > 0);

            baseData.Wrapup();
            newData.Reset();

            Console.WriteLine($"Target's valid: {hasSameTiles}");
        }

    }
}
