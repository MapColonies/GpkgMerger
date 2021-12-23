using System;
using System.Collections.Generic;
using System.IO;
using GpkgMerger.Src.Batching;
using GpkgMerger.Src.Sql;
using GpkgMerger.Src.Utils;

namespace GpkgMerger.Src.DataTypes
{
    public struct Extent
    {
        public double minX;
        public double minY;
        public double maxX;
        public double maxY;
    }

    public struct TileMatrix
    {
        public string tableName;
        public int zoomLevel;
        public int matrixWidth;
        public int matrixHeight;
        public int tileWidth;
        public int tileHeight;
        public double pixleXSize;
        public double pixleYSize;
    }

    public class Gpkg : Data
    {
        private const int ZoomLevelCount = 25;

        private const int CoordsForAllZoomLevels = ZoomLevelCount << 1;

        private string tileCache;

        private int offset;

        private Extent extent;

        public Gpkg(string path, int batchSize) : base(DataType.GPKG, path, batchSize)
        {
            this.tileCache = GpkgSql.GetTileCache(path);
            this.offset = 0;
            this.extent = GpkgSql.GetExtent(path);
        }

        public override void UpdateMetadata(Data data)
        {
            if (data.type != DataType.GPKG)
            {
                return;
            }

            Gpkg gpkg = (Gpkg)data;
            UpdateExtent(gpkg);
            UpdateTileMatrix(gpkg);
        }

        private void UpdateExtent(Gpkg gpkg)
        {
            Extent extent = gpkg.extent;
            Extent combinedExtent = new Extent();

            combinedExtent.minX = Math.Min(this.extent.minX, extent.minX);
            combinedExtent.minY = Math.Min(this.extent.minY, extent.minY);
            combinedExtent.maxX = Math.Max(this.extent.maxX, extent.maxX);
            combinedExtent.maxY = Math.Max(this.extent.maxY, extent.maxY);

            this.extent = combinedExtent;
            GpkgSql.UpdateExtent(this.path, combinedExtent);
        }

        private void UpdateTileMatrix(Gpkg gpkg)
        {
            GpkgSql.CopyTileMatrix(this.path, gpkg.path, this.tileCache);
        }

        public override List<Tile> GetNextBatch()
        {
            List<Tile> tiles = GpkgSql.GetBatch(this.path, this.batchSize, this.offset, this.tileCache);
            this.offset += tiles.Count;
            return tiles;
        }

        public override List<Tile> GetCorrespondingBatch(List<Tile> tiles)
        {
            List<Tile> newTiles = new List<Tile>(this.batchSize);

            foreach (Tile tile in tiles)
            {
                Tile baseTile = GpkgSql.GetTile(this.path, this.tileCache, tile);

                if (baseTile == null)
                {
                    baseTile = GetLastExistingTile(tile);
                }

                newTiles.Add(baseTile);
            }

            return newTiles;
        }

        private Tile GetLastExistingTile(Tile tile)
        {
            int[] coords = new int[CoordsForAllZoomLevels];
            for (int i = 0; i < coords.Length; i++)
            {
                coords[i] = -1;
            }

            int z = tile.Z;
            int baseTileX = tile.X;
            int baseTileY = tile.Y;
            int arrayIterator = 0;
            for (int i = z - 1; i >= 0; i--)
            {
                baseTileX >>= 1; // Divide by 2
                baseTileY >>= 1; // Divide by 2
                arrayIterator = i << 1; // Multiply by 2
                coords[arrayIterator] = baseTileX;
                coords[arrayIterator + 1] = baseTileY;
            }

            Tile lastTile = GpkgSql.GetLastTile(this.path, this.tileCache, coords, tile);
            return lastTile;
        }

        public override void UpdateTiles(List<Tile> tiles)
        {
            GpkgSql.InsertTiles(this.path, this.tileCache, tiles);
        }

        public void PrintBatch(List<Tile> tiles)
        {
            foreach (Tile tile in tiles)
            {
                tile.PrintTile();
            }
        }

        public override void Cleanup()
        {
            GpkgSql.CreateTileIndex(this.path, this.tileCache);
            GpkgSql.Vacuum(this.path);
        }

        public override bool Exists()
        {
            // Get full path to gpkg file
            string fullPath = Path.GetFullPath(this.path);
            return File.Exists(fullPath);
        }
    }
}