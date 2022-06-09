using MergerLogic.Batching;
using MergerLogic.Utils;

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

    public abstract class Data<UtilsType> : IData where UtilsType : IDataUtils
    {
        protected delegate int ValFromCoordFunction(Coord coord);
        protected delegate Tile GetTileFromXYZFunction(int z, int x, int y);
        protected delegate Coord GetCoordFromCoordFunction(Coord coord);
        protected delegate Tile GetTileFromCoordFunction(Coord coord);
        protected delegate Tile TileConvertorFunction(Tile Tile);

        public DataType Type { get; }
        public string Path { get; }
        public readonly bool isOneXOne;
        protected readonly int batchSize;
        public readonly GridOrigin origin;

        protected UtilsType utils;
        protected GetTileFromXYZFunction _getTile;
        protected GetTileFromCoordFunction _getLastExistingTile;

        #region tile grid converters
        protected IOneXOneConvetor _oneXOneConvetor = null;
        protected TileConvertorFunction _fromCurrentGridTile;
        protected GetCoordFromCoordFunction _fromCurrentGridCoord;
        protected TileConvertorFunction _toCurrentGrid;
        #endregion tile grid converters

        //origin converters
        protected TileConvertorFunction _convertOriginTile;
        protected ValFromCoordFunction _convertOriginCoord;


        protected const int ZOOM_LEVEL_COUNT = 30;

        protected const int COORDS_FOR_ALL_ZOOM_LEVELS = ZOOM_LEVEL_COUNT << 1;

        public Data(IUtilsFactory utilsFactory, IOneXOneConvetor oneXOneConvetor, DataType type, string path, int batchSize, bool isOneXOne = false, GridOrigin origin = GridOrigin.UPPER_LEFT)
        {
            this.Type = type;
            this.Path = path;
            this.batchSize = batchSize;
            this.utils = utilsFactory.GetDataUtils<UtilsType>(path);
            this.isOneXOne = isOneXOne;
            this.origin = origin;

            // The following delegates are for code performance and to reduce branching while handling tiles
            if (isOneXOne)
            {
                this._oneXOneConvetor = oneXOneConvetor;
                this._getLastExistingTile = this.getLastOneXoneExistingTile;
                this._fromCurrentGridTile = this._oneXOneConvetor.TryFromTwoXOne;
                this._fromCurrentGridCoord = this._oneXOneConvetor.TryFromTwoXOne;
                this._toCurrentGrid = this._oneXOneConvetor.TryToTwoXOne;
            }
            else
            {
                this._getLastExistingTile = this.GetLastExistingTile;
                this._fromCurrentGridTile = tile => tile;
                this._fromCurrentGridCoord = tile => tile;
                this._toCurrentGrid = tile => tile;
            }
            this._getTile = this.GetTileInitilaizer;
            if (origin == GridOrigin.LOWER_LEFT)
            {
                this._convertOriginTile = tile =>
                {
                    tile.FlipY();
                    return tile;
                };
                this._convertOriginCoord = coord =>
                {
                    return GeoUtils.FlipY(coord);
                };
            }
            else
            {
                this._convertOriginTile = tile => tile;
                this._convertOriginCoord = coord => coord.y;
            }
        }

        public abstract void Reset();

        public virtual void UpdateMetadata(IData data)
        {
            Console.WriteLine($"{this.Type} source, skipping metadata update");
        }

        protected virtual Tile GetLastExistingTile(Coord coords)
        {
            int z = coords.z;
            int baseTileX = coords.x;
            int baseTileY = coords.y;

            Tile lastTile = null;

            // Go over zoom levels until a tile is found (may not find tile)
            for (int i = z - 1; i >= 0; i--)
            {
                baseTileX >>= 1; // Divide by 2
                baseTileY >>= 1; // Divide by 2

                lastTile = this.utils.GetTile(i, baseTileX, baseTileY);
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
            coord.y = this._convertOriginCoord(coord);
            coord = this._fromCurrentGridCoord(coord);

            if (coord is null)
            {
                return false;
            }

            return this.utils.TileExists(coord.z, coord.x, coord.y);
        }

        //TODO: move to util after IOC
        protected Tile getLastOneXoneExistingTile(Coord coords)
        {
            coords = this._oneXOneConvetor.FromTwoXOne(coords);
            Tile? tile = this.GetLastExistingTile(coords);
            return tile != null ? this._oneXOneConvetor.ToTwoXOne(tile) : null;
        }

        protected virtual Tile GetOneXOneTile(int z, int x, int y)
        {
            Coord? oneXoneBaseCoords = this._oneXOneConvetor.TryFromTwoXOne(z, x, y);
            if (oneXoneBaseCoords == null)
            {
                return null;
            }
            Tile tile = this.utils.GetTile(oneXoneBaseCoords);
            return tile != null ? this._oneXOneConvetor.ToTwoXOne(tile) : null;
        }


        //lazy load get tile function on first call for compatibility with null utills in contractor
        protected Tile GetTileInitilaizer(int z, int x, int y)
        {
            GetTileFromXYZFunction fixedGridGetTileFuntion = this.isOneXOne ? this.GetOneXOneTile : this.utils.GetTile;
            if (this.origin == GridOrigin.LOWER_LEFT)
            {
                this._getTile = (z, x, y) =>
                {
                    int newY = GeoUtils.FlipY(z, y);
                    Tile tile = fixedGridGetTileFuntion(z, x, newY);
                    //set cords to current origin
                    tile.SetCoords(z, x, y);
                    return tile;
                };
            }
            else
            {
                this._getTile = fixedGridGetTileFuntion;
            }
            return this._getTile(z, x, y);
        }

        public abstract List<Tile> GetNextBatch(out string batchIdentifier);

        public Tile GetCorrespondingTile(Coord coords, bool upscale)
        {
            Tile correspondingTile = this._getTile(coords.z, coords.x, coords.y);

            if (upscale && correspondingTile == null)
            {
                correspondingTile = this._getLastExistingTile(coords);
            }
            return correspondingTile;
        }

        public void UpdateTiles(IEnumerable<Tile> tiles)
        {
            var targetTiles = tiles.Select(tile =>
            {
                var targetTile = this._convertOriginTile(tile);
                targetTile = this._fromCurrentGridTile(targetTile);
                return targetTile;
            }).Where(tile => tile != null);
            this.InternalUpdateTiles(targetTiles);
        }

        protected abstract void InternalUpdateTiles(IEnumerable<Tile> targetTiles);

        public virtual void Wrapup()
        {
            Console.WriteLine($"{this.Type} source, skipping wrapup phase");
        }

        public abstract bool Exists();

        public abstract int TileCount();

        public abstract void setBatchIdentifier(string batchIdentifier);

    }
}
