using System;
using System.Collections.Generic;
using GpkgMerger.Src.Batching;
using GpkgMerger.Src.Sql;

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
        private string tileCache;
        private int batchSize;
        private int offset;
        private Extent extent;

        public Gpkg(DataType type, string path, int batchSize)
        {
            this.tileCache = GpkgSql.GetTileCache(path);
            this.type = type;
            this.path = path;
            this.batchSize = batchSize;
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
                Tile newTile = GpkgSql.GetTile(this.path, this.tileCache, tile);
                newTiles.Add(newTile);
            }

            return newTiles;
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
    }
}