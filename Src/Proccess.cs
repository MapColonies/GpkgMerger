using System;
using System.Collections.Generic;
using GpkgMerger.Src.DataTypes;
using GpkgMerger.Src.Batching;
using GpkgMerger.Src.ImageProccessing;

namespace GpkgMerger.Src
{
    public static class Proccess
    {

        public static void Start(Data baseData, Data newData)
        {
            List<Tile> tiles;
            int totalTileCount = 0;

            // Update base metadata according to new data
            baseData.UpdateMetadata(newData);

            do
            {
                List<Tile> newTiles = newData.GetNextBatch();
                List<Tile> baseTiles = baseData.GetCorrespondingBatch(newTiles);

                tiles = new List<Tile>();

                for (int i = 0; i < newTiles.Count; i++)
                {
                    Tile newTile = newTiles[i];
                    Tile baseTile = baseTiles[i];

                    if (baseTile != null)
                    {
                        string blob = Merge.MergeNewToBase(newTile, baseTile);
                        newTile = new Tile(newTile.Z, newTile.X, newTile.Y, blob, blob.Length);
                    }

                    tiles.Add(newTile);
                }

                totalTileCount += tiles.Count;
                Console.WriteLine($"Tile Count: {totalTileCount}");

                baseData.UpdateTiles(tiles);
            } while (tiles.Count > 0);

            baseData.Cleanup();
        }
    }
}