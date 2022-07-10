using MergerLogic.Batching;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MergerLogic.DataTypes
{
    public enum DataType
    {
        GPKG,
        FOLDER,
        S3,
        WMTS,
        TMS,
        XYZ
    }

    public enum GridOrigin
    {
        LOWER_LEFT,
        UPPER_LEFT
    }

    public abstract class Data<TUtilsType> : IData where TUtilsType : IDataUtils
    {
        protected delegate int ValFromCoordFunction(Coord coord);
        protected delegate Tile GetTileFromXyzFunction(int z, int x, int y);
        protected delegate Coord GetCoordFromCoordFunction(Coord coord);
        protected delegate Tile GetTileFromCoordFunction(Coord coord);
        protected delegate Tile TileConvertorFunction(Tile tile);

        public DataType Type { get; }
        public string Path { get; }
        public bool IsOneXOne { get; }
        public GridOrigin Origin { get; }
        protected readonly int BatchSize;

        protected TUtilsType Utils;
        protected GetTileFromXyzFunction GetTile;
        protected readonly GetTileFromCoordFunction GetLastExistingTile;
        protected readonly IGeoUtils GeoUtils;
        protected readonly ILogger _logger;

        #region tile grid converters
        protected IOneXOneConvertor OneXOneConvertor = null;
        protected TileConvertorFunction FromCurrentGridTile;
        protected GetCoordFromCoordFunction FromCurrentGridCoord;
        protected TileConvertorFunction ToCurrentGrid;
        #endregion tile grid converters

        //origin converters
        protected TileConvertorFunction ConvertOriginTile;
        protected ValFromCoordFunction ConvertOriginCoord;

        protected Data(IServiceProvider container, DataType type, string path, int batchSize, bool isOneXOne = false, GridOrigin origin = GridOrigin.UPPER_LEFT)
        {
            this.Type = type;
            this.Path = path;
            this.BatchSize = batchSize;
            var utilsFactory = container.GetRequiredService<IUtilsFactory>();
            this.Utils = utilsFactory.GetDataUtils<TUtilsType>(path);
            this.GeoUtils = container.GetRequiredService<IGeoUtils>();
            this.IsOneXOne = isOneXOne;
            this.Origin = origin;
            var loggerFactory = container.GetRequiredService<ILoggerFactory>();
            this._logger = loggerFactory.CreateLogger(this.GetType());

            // The following delegates are for code performance and to reduce branching while handling tiles
            if (isOneXOne)
            {
                this.OneXOneConvertor = container.GetRequiredService<IOneXOneConvertor>();
                this.GetLastExistingTile = this.GetLastOneXOneExistingTile;
                this.FromCurrentGridTile = this.OneXOneConvertor.TryFromTwoXOne;
                this.FromCurrentGridCoord = this.OneXOneConvertor.TryFromTwoXOne;
                this.ToCurrentGrid = this.OneXOneConvertor.TryToTwoXOne;
            }
            else
            {
                this.GetLastExistingTile = this.InternalGetLastExistingTile;
                this.FromCurrentGridTile = tile => tile;
                this.FromCurrentGridCoord = tile => tile;
                this.ToCurrentGrid = tile => tile;
            }
            this.GetTile = this.GetTileInitializer;
            if (origin == GridOrigin.LOWER_LEFT)
            {
                this.ConvertOriginTile = tile =>
                {
                    tile.Y = this.GeoUtils.FlipY(tile);
                    return tile;
                };
                this.ConvertOriginCoord = coord =>
                {
                    return this.GeoUtils.FlipY(coord);
                };
            }
            else
            {
                this.ConvertOriginTile = tile => tile;
                this.ConvertOriginCoord = coord => coord.Y;
            }
        }

        public abstract void Reset();

        protected virtual Tile InternalGetLastExistingTile(Coord coords)
        {
            int z = coords.Z;
            int baseTileX = coords.X;
            int baseTileY = coords.Y;

            Tile lastTile = null;

            // Go over zoom levels until a tile is found (may not find tile)
            for (int i = z - 1; i >= 0; i--)
            {
                baseTileX >>= 1; // Divide by 2
                baseTileY >>= 1; // Divide by 2

                lastTile = this.Utils.GetTile(i, baseTileX, baseTileY);
                if (lastTile != null)
                {
                    break;
                }
            }

            return lastTile;
        }

        public bool TileExists(Tile tile)
        {
            return this.TileExists(tile.GetCoord());
        }

        public bool TileExists(Coord coord)
        {
            coord.Y = this.ConvertOriginCoord(coord);
            coord = this.FromCurrentGridCoord(coord);

            if (coord is null)
            {
                return false;
            }

            return this.Utils.TileExists(coord.Z, coord.X, coord.Y);
        }

        //TODO: move to util after IOC
        protected Tile GetLastOneXOneExistingTile(Coord coords)
        {
            coords = this.FromCurrentGridCoord(coords);
            if (coords is null)
            {
                return null;
            }
            Tile? tile = this.InternalGetLastExistingTile(coords);
            return tile != null ? this.OneXOneConvertor.ToTwoXOne(tile) : null;
        }

        protected virtual Tile GetOneXOneTile(int z, int x, int y)
        {
            Coord? oneXoneBaseCoords = this.OneXOneConvertor.TryFromTwoXOne(z, x, y);
            if (oneXoneBaseCoords == null)
            {
                return null;
            }
            Tile tile = this.Utils.GetTile(oneXoneBaseCoords);
            return tile != null ? this.OneXOneConvertor.ToTwoXOne(tile) : null;
        }


        //lazy load get tile function on first call for compatibility with null utills in contractor
        protected Tile GetTileInitializer(int z, int x, int y)
        {
            GetTileFromXyzFunction fixedGridGetTileFunction = this.IsOneXOne ? this.GetOneXOneTile : this.Utils.GetTile;
            if (this.Origin == GridOrigin.LOWER_LEFT)
            {
                this.GetTile = (z, x, y) =>
                {
                    int newY = this.GeoUtils.FlipY(z, y);
                    Tile? tile = fixedGridGetTileFunction(z, x, newY);
                    //set cords to current origin
                    tile?.SetCoords(z, x, y);
                    return tile;
                };
            }
            else
            {
                this.GetTile = fixedGridGetTileFunction;
            }
            return this.GetTile(z, x, y);
        }

        public abstract List<Tile> GetNextBatch(out string batchIdentifier);

        public Tile GetCorrespondingTile(Coord coords, bool upscale)
        {
            Tile correspondingTile = this.GetTile(coords.Z, coords.X, coords.Y);

            if (upscale && correspondingTile == null)
            {
                correspondingTile = this.GetLastExistingTile(coords);
            }
            return correspondingTile;
        }

        public void UpdateTiles(IEnumerable<Tile> tiles)
        {
            var targetTiles = tiles.Select(tile =>
            {
                var targetTile = this.ConvertOriginTile(tile);
                targetTile = this.FromCurrentGridTile(targetTile);
                return targetTile;
            }).Where(tile => tile != null);
            this.InternalUpdateTiles(targetTiles);
        }

        protected abstract void InternalUpdateTiles(IEnumerable<Tile> targetTiles);

        public virtual void Wrapup()
        {
            this.Reset();
            this._logger.LogInformation($"{this.Type} source, skipping wrapup phase");
        }

        public abstract bool Exists();

        public abstract int TileCount();

        public abstract void setBatchIdentifier(string batchIdentifier);

    }
}
