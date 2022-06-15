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

    public class Gpkg : Data<IGpkgUtils>
    {
        private delegate Coord CoordConvertorFunction(Coord cords);

        private string tileCache;

        private int offset;

        private CoordConvertorFunction _coordsFromCurrentGrid;
        private IConfigurationManager _configManager;

        public Gpkg(IConfigurationManager configuration, IServiceProvider container,
            string path, int batchSize, bool isBase = false, bool isOneXOne = false, Extent? extent = null, int? maxZoom = null, GridOrigin origin = GridOrigin.UPPER_LEFT)
            : base(container, DataType.GPKG, path, batchSize, isOneXOne, origin)
        {
            this.tileCache = this.utils.GetTileCache();
            this.offset = 0;
            this._configManager = configuration;

            if (isOneXOne)
            {
                this._coordsFromCurrentGrid = this._oneXOneConvetor.TryFromTwoXOne;
            }
            else
            {
                this._coordsFromCurrentGrid = cords => cords;
            }
            if (!this.utils.Exist() && isBase && maxZoom is not null && extent is not null)
            {
                this.utils.Create(extent.Value, maxZoom.Value, isOneXOne);
            }
        }

        public override void Reset()
        {
            this.offset = 0;
        }

        public override void UpdateMetadata(IData data) //TODO: check if should be moved to wrapup?
        {
            //TODO: support update old gpkg with known extent 
            if (data.Type != DataType.GPKG)
            {
                return;
            }

            Gpkg gpkg = (Gpkg)data;
            this.UpdateExtent(gpkg);
            this.UpdateTileMatrix(gpkg); //TODO: replace with sql based update by zoom level
        }

        private void UpdateExtent(Gpkg gpkg)
        {
            Extent baseExtent = this.utils.GetExtent();
            Extent extent = gpkg.utils.GetExtent();
            Extent combinedExtent = new Extent();

            combinedExtent.minX = Math.Min(baseExtent.minX, extent.minX);
            combinedExtent.minY = Math.Min(baseExtent.minY, extent.minY);
            combinedExtent.maxX = Math.Max(baseExtent.maxX, extent.maxX);
            combinedExtent.maxY = Math.Max(baseExtent.maxY, extent.maxY);

            this.utils.UpdateExtent(combinedExtent);
        }

        private void UpdateTileMatrix(Gpkg gpkg)
        {
            GpkgUtils.CopyTileMatrix(this.Path, gpkg.Path, this.tileCache);
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            batchIdentifier = this.offset.ToString();
            //TODO: optimize after IOC refactoring
            int counter = 0;
            List<Tile> tiles = this.utils.GetBatch(this.batchSize, this.offset)
                .Select(t =>
                {
                    Tile tile = this._convertOriginTile(t);
                    tile = this._toCurrentGrid(tile);
                    counter++;
                    return tile;
                }).Where(t => t != null).ToList();
            this.offset += counter;
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

            Tile lastTile = this.utils.GetLastTile(coords, baseCoords);
            if (lastTile is not null)
            {
                lastTile = this._toCurrentGrid(lastTile);
            }
            return lastTile;
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
            //TODO: update extent and tile grid?
            this.utils.CreateTileIndex();

            bool vacuum = bool.Parse(this._configManager.GetConfiguration("GPKG", "vacuum"));
            if (vacuum)
            {
                this.utils.Vacuum();
            }

            this.Reset();
        }

        public override bool Exists()
        {
            Console.WriteLine($"Checking if exists, gpkg: {this.Path}");
            return this.utils.Exist();
        }

        public override int TileCount()
        {
            return this.utils.GetTileCount();
        }

        protected override void InternalUpdateTiles(IEnumerable<Tile> targetTiles)
        {
            this.utils.InsertTiles(targetTiles);
        }
    }
}
