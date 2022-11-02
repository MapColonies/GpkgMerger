using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class Gpkg : Data<IGpkgClient>
    {
        private long _offset;
        private readonly IConfigurationManager _configManager;

        public Gpkg(IConfigurationManager configuration, IServiceProvider container,
            string path, int batchSize, Grid? grid, GridOrigin? origin, bool isBase = false, Extent? extent = null)
            : base(container, DataType.GPKG, path, batchSize, grid, origin, isBase, extent)
        {
            this._offset = 0;
            this._configManager = configuration;

            if (isBase)
            {
                this.Utils.DeleteTileTableTriggers();
                this.Utils.UpdateExtent(this.Extent);
            }
        }

        protected override void Create()
        {
            this.Utils.Create(this.Extent, this.IsOneXOne);
        }

        protected override void Validate() {
            if (!this.Utils.IsValidGrid(this.IsOneXOne))
            {
                var gridType = this.IsOneXOne ? "1X1" : "2X1";
                throw new Exception($"gpkg source {this.Path} don't have valid {gridType} grid.");
            }
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.UPPER_LEFT;
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
            List<Tile> tiles = this.Utils.GetBatch(this.BatchSize, this._offset)
                .Select(t =>
                {
                    Tile tile = this.ConvertOriginTile(t);
                    tile = this.ToCurrentGrid(tile);
                    counter++;
                    return tile;
                }).Where(t => t != null).ToList();
            this._offset += counter;
            return tiles;
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this._offset = long.Parse(batchIdentifier);
        }

        protected override Tile InternalGetLastExistingTile(Coord baseCoords)
        {
            int cordsLength = baseCoords.Z << 1;
            int[] coords = new int[cordsLength];

            int z = baseCoords.Z;
            int baseTileX = baseCoords.X;
            int baseTileY = this.ConvertOriginCoord(baseCoords);
            int arrayIterator = 0;
            for (int i = z - 1; i >= 0; i--)
            {
                baseTileX >>= 1; // Divide by 2
                baseTileY >>= 1; // Divide by 2
                arrayIterator = i << 1; // Multiply by 2
                coords[arrayIterator] = baseTileX;
                coords[arrayIterator + 1] = baseTileY;
            }

            Tile lastTile = this.Utils.GetLastTile(coords, z);
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
            this.Utils.UpdateTileMatrixTable(this.IsOneXOne);
            this.Utils.CreateTileCacheValidationTriggers();

            bool vacuum = this._configManager.GetConfiguration<bool>("GPKG", "vacuum");
            if (vacuum)
            {
                this.Utils.Vacuum();
            }

            this.Reset();
        }

        public override bool Exists()
        {
            return this.Utils.Exist();
        }

        public override long TileCount()
        {
            return this.Utils.GetTileCount();
        }

        protected override void InternalUpdateTiles(IEnumerable<Tile> targetTiles)
        {
            this.Utils.InsertTiles(targetTiles);
        }
    }
}
