using System;
using System.Collections.Generic;
using GpkgMerger.Src.Batching;
using GpkgMerger.Src.DataTypes;
using GpkgMerger.Src.ImageProccessing;

namespace GpkgMerger.Src
{
    public static class Proccess
    {

        public static void Start(Data baseData, Data newData, int batchSize)
        {
            List<Tile> tiles = new List<Tile>(batchSize);
            int totalTileCount = newData.TileCount();
            int tileProgressCount = 0;

            Console.WriteLine($"Total amount of tiles to merge: {totalTileCount}");

            // Update base metadata according to new data
            baseData.UpdateMetadata(newData);

            do
            {
                List<Tile> newTiles = newData.GetNextBatch();
                List<Tile> baseTiles = baseData.GetUpscaledCorrespondingBatch(newTiles);

                tiles.Clear();
                for (int i = 0; i < newTiles.Count; i++)
                {
                    Tile newTile = newTiles[i];
                    Tile baseTile = baseTiles[i];

                    if (baseTile != null)
                    {
                        string blob = Merge.MergeTiles(new[] { newTile, baseTile });
                        newTile = new Tile(newTile.Z, newTile.X, newTile.Y, blob, blob.Length);
                    }

                    tiles.Add(newTile);
                }

                tileProgressCount += tiles.Count;
                Console.WriteLine($"Tile Count: {tileProgressCount} / {totalTileCount}");

                baseData.UpdateTiles(tiles);
            } while (tiles.Count == batchSize);

            baseData.Wrapup();
        }

        public static void Validate(Data baseData, Data newData)
        {
            List<Tile> newTiles;
            bool hasSameTiles = true;

            int totalTileCount = newData.TileCount();
            Console.WriteLine($"Base tile Count: {baseData.TileCount()}, New tile count: {newData.TileCount()}");

            do
            {
                newTiles = newData.GetNextBatch();
                List<Tile> baseTiles = baseData.GetCorrespondingBatch(newTiles);

                int baseMatchCount = 0;
                int newTileCount = 0;

                for (int i = 0; i < newTiles.Count; i++)
                {
                    Tile newTile = newTiles[i];
                    Tile baseTile = baseTiles[i];

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
                Console.WriteLine($"Base tile Count: {newTileCount}, New tile count: {baseMatchCount}");
                hasSameTiles = newTileCount == baseMatchCount;

            } while (hasSameTiles && newTiles.Count > 0);

            baseData.Wrapup();

            Console.WriteLine($"Target's valid: {hasSameTiles}");
        }
    }
}
