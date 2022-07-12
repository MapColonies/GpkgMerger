using MergerLogic.Batching;
using MergerLogic.Utils;
using Microsoft.Extensions.Logging;

namespace MergerLogic.DataTypes
{
    public class Gpkg : Data<IGpkgUtils>
    {
        private int _offset;

        private readonly IConfigurationManager _configManager;

        public Gpkg(IConfigurationManager configuration, IServiceProvider container,
            string path, int batchSize, bool isBase = false, bool isOneXOne = false, Extent? extent = null, GridOrigin origin = GridOrigin.UPPER_LEFT)
            : base(container, DataType.GPKG, path, batchSize, isOneXOne, origin)
        {
            this._offset = 0;
            this._configManager = configuration;

            if (isBase)
            {
                if (extent is null)
                {
                    //throw error if extent is missing in base
                    throw new Exception($" base gpkg '{path}' must have extent");
                }

                if (!this.Utils.Exist())
                {
                    this.Utils.Create(extent.Value, isOneXOne);
                }
                else
                {
                    this.Utils.DeleteTileTableTriggers();
                }
                this.Utils.UpdateExtent(extent.Value);
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
            this._offset = int.Parse(batchIdentifier);
        }

        protected override Tile InternalGetLastExistingTile(Coord baseCoords)
        {
            int cordsLength = baseCoords.Z << 1;
            int[] coords = new int[cordsLength];

            int z = baseCoords.Z;
            int baseTileX = baseCoords.X;
            int baseTileY = baseCoords.Y;
            int arrayIterator = 0;
            for (int i = z - 1; i >= 0; i--)
            {
                baseTileX >>= 1; // Divide by 2
                baseTileY >>= 1; // Divide by 2
                arrayIterator = i << 1; // Multiply by 2
                coords[arrayIterator] = baseTileX;
                coords[arrayIterator + 1] = baseTileY;
            }

            Tile lastTile = this.Utils.GetLastTile(coords, baseCoords);
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
            this.Utils.CreateTileIndex();
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
            this._logger.LogInformation($"Checking if exists, gpkg: {this.Path}");
            return this.Utils.Exist();
        }

        public override int TileCount()
        {
            return this.Utils.GetTileCount();
        }

        protected override void InternalUpdateTiles(IEnumerable<Tile> targetTiles)
        {
            this.Utils.InsertTiles(targetTiles);
        }
    }
}
