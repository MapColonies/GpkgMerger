using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class Gpkg : Data<IGpkgClient>
    {
        private long _offset;
        private Extent _extent;
        private readonly IConfigurationManager _configManager;
        static readonly object _locker = new object();

        public Gpkg(IConfigurationManager configuration, IServiceProvider container,
            string path, int batchSize, Grid? grid, GridOrigin? origin, bool isBase = false, Extent? extent = null)
            : base(container, DataType.GPKG, path, batchSize, grid, origin, isBase, extent)
        {
            this._offset = 0;
            this._configManager = configuration;

            if (isBase)
            {
                this.Utils.DeleteTileTableTriggers();
                this.Utils.UpdateExtent(this._extent);
            }
            else
            {
                this._extent = this.Utils.GetExtent();
            }
        }

        protected override void SetExtent(Extent? extent)
        {
            if (this.IsBase)
            {
                if (extent is null)
                {
                    //throw error if extent is missing in base
                    throw new Exception($"base {this.Type} '{this.Path}' must have extent");
                }
                this._extent = extent.Value;
            }
            else
            {
                this._extent = base.GetExtent();
            }
        }

        protected override Extent GetExtent()
        {
            return this._extent;
        }

        protected override void Create()
        {
            this.Utils.Create(this._extent, this.IsOneXOne);
        }

        protected override void Validate() {
            if (!this.Utils.IsValidGrid(this.IsOneXOne))
            {
                var gridType = this.IsOneXOne ? "1X1" : "2X1";
                throw new Exception($"{this.Type} source {this.Path} don't have valid {gridType} grid.");
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

        public override List<Tile> GetNextBatch(out string currentBatchIdentifier, out string? nextBatchIdentifier, string? inCompletedBatchIdentifier, long? totalTilesCount)
        {
            lock (_locker)
            {
                if (inCompletedBatchIdentifier is not null)
                {
                    long.TryParse(inCompletedBatchIdentifier, out long result);
                    this._offset = result;
                }
                currentBatchIdentifier = this._offset.ToString();
                List<Tile> tiles = new List<Tile>();
                if (this._offset != totalTilesCount)
                {
                    //TODO: optimize after IOC refactoring
                    int counter = 0;

                    tiles = this.Utils.GetBatch(this.BatchSize, this._offset)
                        .Select(t =>
                        {
                            Tile tile = this.ConvertOriginTile(t);
                            tile = this.ToCurrentGrid(tile);
                            counter++;
                            return tile;
                        }).Where(t => t != null).ToList();
                    if (inCompletedBatchIdentifier is null)
                    {
                        Interlocked.Add(ref this._offset, counter);
                    }
                    nextBatchIdentifier = this._offset.ToString();
                    return tiles;
                }
                nextBatchIdentifier = this._offset.ToString();
                return tiles;
            }
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            lock (_locker)
            {
                this._offset = long.Parse(batchIdentifier);
            }
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
