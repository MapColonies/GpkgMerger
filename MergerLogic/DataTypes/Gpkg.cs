using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
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
        private delegate Tile TileConvertorFunction(Tile Tile);
        private delegate Coord CoordConvertorFunction(Coord cords);

        private string tileCache;

        private int offset;

        private Extent extent;
        private TileConvertorFunction _toCurrentGrid;
        private TileConvertorFunction _toSourceGrid;
        private CoordConvertorFunction _coordsFromCurrentGrid;

        public Gpkg(string path, int batchSize, bool isOneXOne = false) : base(DataType.GPKG, path, batchSize, new GpkgUtils(path), isOneXOne)
        {
            this.tileCache = GpkgUtils.GetTileCache(path);
            this.offset = 0;
            this.extent = GpkgUtils.GetExtent(path);

            if (isOneXOne)
            {
                this._toCurrentGrid = this._oneXOneConvetor.TryToTwoXOne;
                this._coordsFromCurrentGrid = this._oneXOneConvetor.TryFromTwoXOne;
                this._toSourceGrid = this._oneXOneConvetor.TryFromTwoXOne;
            }
            else
            {
                this._toCurrentGrid = tile => tile;
                this._coordsFromCurrentGrid = cords => cords;
                this._toSourceGrid = tile => tile;
            };
        }

        public override void Reset()
        {
            this.offset = 0;
        }

        public override void UpdateMetadata(Data data)
        {
            if (data.type != DataType.GPKG)
            {
                return;
            }

            Gpkg gpkg = (Gpkg)data;
            this.UpdateExtent(gpkg);
            this.UpdateTileMatrix(gpkg);
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
            GpkgUtils.UpdateExtent(this.path, combinedExtent);
        }

        private void UpdateTileMatrix(Gpkg gpkg)
        {
            GpkgUtils.CopyTileMatrix(this.path, gpkg.path, this.tileCache);
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            batchIdentifier = this.offset.ToString();
            //TODO: optimize after IOC refactoring
            List<Tile> tiles = GpkgUtils.GetBatch(this.path, this.batchSize, this.offset, this.tileCache).Select(t => this._toCurrentGrid(t)).ToList();
            this.offset += tiles.Count;
            return tiles;
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this.offset = int.Parse(batchIdentifier);
        }

        protected override Tile GetLastExistingTile(Coord baseCoords)
        {
            int[] coords = new int[COORDS_FOR_ALL_ZOOM_LEVELS];
            for (int i = 0; i < coords.Length; i++)
            {
                coords[i] = -1;
            }

            baseCoords = this._coordsFromCurrentGrid(baseCoords);
            int z = baseCoords.z;
            int baseTileX = baseCoords.x;
            int baseTileY = baseCoords.y;
            int arrayIterator = 0;
            for (int i = z - 1; i >= 0; i--)
            {
                baseTileX >>= 1; // Divide by 2
                baseTileY >>= 1; // Divide by 2
                arrayIterator = i << 1; // Multiply by 2
                coords[arrayIterator] = baseTileX;
                coords[arrayIterator + 1] = baseTileY;
            }

            Tile lastTile = GpkgUtils.GetLastTile(this.path, this.tileCache, coords, baseCoords);
            return this._toCurrentGrid(lastTile);
        }

        public override void UpdateTiles(List<Tile> tiles)
        {
            //TODO: optimize after IOC refactoring
            tiles = tiles.Select(tile => this._toSourceGrid(tile)).ToList();
            GpkgUtils.InsertTiles(this.path, this.tileCache, tiles);
        }

        public void PrintBatch(List<Tile> tiles)
        {
            foreach (Tile tile in tiles)
            {
                tile.Print();
            }
        }

        public override void Wrapup()
        {
            GpkgUtils.CreateTileIndex(this.path, this.tileCache);

            bool vacuum = bool.Parse(Configuration.Instance.GetConfiguration("GPKG", "vacuum"));
            if (vacuum)
            {
                GpkgUtils.Vacuum(this.path);
            }

            this.Reset();
        }

        public override bool Exists()
        {
            Console.WriteLine($"Checking if exists, gpkg: {this.path}");
            // Get full path to gpkg file
            string fullPath = Path.GetFullPath(this.path);
            return File.Exists(fullPath);
        }

        public override int TileCount()
        {
            return GpkgUtils.GetTileCount(this.path, this.tileCache);
        }
    }
}
