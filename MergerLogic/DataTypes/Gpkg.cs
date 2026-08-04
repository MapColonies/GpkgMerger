using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.Utils;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MergerLogic.DataTypes
{
    public class Gpkg : Data<IGpkgClient>
    {
        // Keyset cursor: the greatest tile id handed out so far. Persisted as the batch identifier for resume.
        private long _batchCursorId;
        private Extent _extent;
        private readonly IConfigurationManager _configManager;
        static readonly object _locker = new object();

        public Gpkg(IConfigurationManager configuration, IServiceProvider container,
            string path, int batchSize, Grid? grid, GridOrigin? origin, bool isBase = false, Extent? extent = null)
            : base(container, DataType.GPKG, path, batchSize, grid, origin, isBase, extent)
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] Ctor started");
            this._batchCursorId = 0;
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
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] Ctor ended");
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
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] started");
            this.Utils.Create(this._extent, this.IsOneXOne);
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] ended");
        }

        protected override void Validate()
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] started");
            if (!this.Utils.IsValidGrid(this.IsOneXOne))
            {
                var gridType = this.IsOneXOne ? "1X1" : "2X1";
                throw new Exception($"{this.Type} source {this.Path} don't have valid {gridType} grid.");
            }
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] ended");
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.UPPER_LEFT;
        }

        public override void Reset()
        {
            lock (_locker)
            {
                this._batchCursorId = 0;
            }
        }

        public override List<Tile> GetNextBatch(out string currentBatchIdentifier, out string? nextBatchIdentifier, long? totalTilesCount)
        {
            lock (_locker)
            {
                this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] started");
                currentBatchIdentifier = this._batchCursorId.ToString();

                var (rawTiles, cursor) = this.Utils.GetBatch(this.BatchSize, this._batchCursorId);
                List<Tile> tiles = rawTiles
                    .Select(t =>
                    {
                        Tile tile = this.ConvertOriginTile(t);
                        tile = this.ToCurrentGrid(tile);
                        return tile;
                    }).Where(t => t != null).ToList();

                this._batchCursorId = cursor;
                nextBatchIdentifier = this._batchCursorId.ToString();
                this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] ended");
                return tiles;
            }
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            lock (_locker)
            {
                this._batchCursorId = long.Parse(batchIdentifier);
            }
        }

        protected override Tile? InternalGetLastExistingTile(Coord baseCoords)
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] started for coord: z:{baseCoords.Z}, x:{baseCoords.X}, y:{baseCoords.Y}");
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

            Tile? lastTile = this.Utils.GetLastTile(coords, z);
            string message = lastTile == null ? "null" : $"z:{lastTile.Z}, x:{lastTile.X}, y:{lastTile.Y}";
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] ended, lastTile {message}");
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
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] started");
            this.Utils.UpdateTileMatrixTable(this.IsOneXOne);
            this.Utils.CreateTileCacheValidationTriggers();

            bool vacuum = this._configManager.GetConfiguration<bool>("GPKG", "vacuum");
            if (vacuum)
            {
                this.Utils.Vacuum();
            }

            this.Reset();
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] ended");
        }

        public override bool Exists()
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] started");
            bool exists = this.Utils.Exist();
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] ended");
            return exists;
        }

        public override long TileCount()
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] started");
            long result = this.Utils.GetTileCount();
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] ended");
            return result;
        }

        protected override void InternalUpdateTiles(IEnumerable<Tile> targetTiles)
        {
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] started");
            this.Utils.InsertTiles(targetTiles);
            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod()?.Name}] ended");
        }
    }
}
