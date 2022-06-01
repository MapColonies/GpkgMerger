using MergerCli.Utils;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProccessing;

namespace MergerCli
{
    internal static class Proccess
    {

        public static void Start(Data baseData, Data newData, int batchSize, BatchStatusManager batchStatusManager)
        {
            batchStatusManager.InitilaizeLayer(newData.path);
            List<Tile> tiles = new List<Tile>(batchSize);
            int totalTileCount = newData.TileCount();
            int tileProgressCount = 0;

            string? resumeBatchIdentifier = batchStatusManager.GetLayerBatchIdentifier(newData.path);
            if (resumeBatchIdentifier != null)
            {
                newData.setBatchIdentifier(resumeBatchIdentifier);
                // fix resume progress bug for gpkg, fs and web, fixing it for s3 requires storing additional data.
                if (newData.type != DataType.S3)
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
                batchStatusManager.SetCurrentBatch(newData.path, batchIdentifier);

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

                    byte[]? image = Merge.MergeTiles(correspondingTileBuilders, targetCoords);

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

            batchStatusManager.CompleteLayer(newData.path);
            baseData.Wrapup();
            newData.Reset();
        }

        public static void Validate(Data baseData, Data newData)
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
                    Tile baseTile = baseData.GetCorrespondingTile(newTile.GetCoord(), false);

                    if (baseTile != null)
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
