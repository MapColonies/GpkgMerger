using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class Gpkg : Data<IGpkgUtils>
    {
        private delegate Coord CoordConvertorFunction(Coord cords);

        private int _offset;

        private CoordConvertorFunction _coordsFromCurrentGrid;
        private IConfigurationManager _configManager;

        public Gpkg(IConfigurationManager configuration, IServiceProvider container,
            string path, int batchSize, bool isBase = false, bool isOneXOne = false, Extent? extent = null, GridOrigin origin = GridOrigin.UPPER_LEFT)
            : base(container, DataType.GPKG, path, batchSize, isOneXOne, origin)
        {
            this._offset = 0;
            this._configManager = configuration;

            if (isOneXOne)
            {
                this._coordsFromCurrentGrid = this.OneXOneConvertor.TryFromTwoXOne;
            }
            else
            {
                this._coordsFromCurrentGrid = cords => cords;
            }
            if (isBase)
            {
                if (extent is null)
                {
                    //throw error if extent is missing in base
                    throw new Exception($" base gpkg '{path}' must have extent");
                }

                if (!this.utils.Exist())
                {
                    this.utils.Create(extent.Value, isOneXOne);
                }
                else
                {
                    this.utils.DeleteTileTableTriggers();
                }
                this.utils.UpdateExtent(extent.Value);
            }
        }

        public override void Reset()
        {
            this._offset = 0;
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            batchIdentifier = this._offset.ToString();
            //TODO: optimize after IOC refactoring
            int counter = 0;
            List<Tile> tiles = this.utils.GetBatch(this.batchSize, this._offset)
                .Select(t =>
                {
                    Tile tile = this._convertOriginTile(t);
                    tile = this._toCurrentGrid(tile);
                    counter++;
                    return tile;
                }).Where(t => t != null).ToList();
            this._offset += counter;
            return tiles;
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this._offset = int.Parse(batchIdentifier);
        }

        protected override Tile GetLastExistingTile(Coord baseCoords)
        {
            int cordsLength = baseCoords.z << 1;
            int[] coords = new int[cordsLength];

            //baseCoords = this._coordsFromCurrentGrid(baseCoords);
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
            //if (lastTile is not null)
            //{
            //    lastTile = this._toCurrentGrid(lastTile);
            //}
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
            this.utils.CreateTileIndex();
            this.utils.UpdateTileMatrixTable(this.isOneXOne);
            this.utils.CreateTileCacheValidationTriggers();

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
