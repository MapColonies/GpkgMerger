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

    public abstract class Data
    {
        protected delegate Tile GetTileFromXYZFunction(int z, int x, int y);
        protected delegate Tile GetTileFromCoordFunction(Coord coords);
        protected delegate Tile TileConvertorFunction(Tile Tile);

        public readonly DataType type;
        public readonly string path;
        public readonly bool isOneXOne;
        protected readonly int batchSize;
        public readonly TileGridOrigin origin;

        protected DataUtils utils;
        protected GetTileFromXYZFunction _getTile;
        protected GetTileFromCoordFunction _getLastExistingTile;

        // tile grid converters
        protected OneXOneConvetor _oneXOneConvetor = null;
        protected TileConvertorFunction _fromCurrentGrid;
        protected TileConvertorFunction _toCurrentGrid;

        //origin converters
        protected TileConvertorFunction _convertOrigin;


        protected const int ZOOM_LEVEL_COUNT = 30;

        protected const int COORDS_FOR_ALL_ZOOM_LEVELS = ZOOM_LEVEL_COUNT << 1;

        public Data(DataType type, string path, int batchSize, DataUtils utils, bool isOneXOne = false, TileGridOrigin origin = TileGridOrigin.UPPER_LEFT)
        {
            this.type = type;
            this.path = path;
            this.batchSize = batchSize;
            this.utils = utils;
            this.isOneXOne = isOneXOne;
            this.origin = origin;
            if (isOneXOne)
            {
                this._oneXOneConvetor = new OneXOneConvetor();
                this._getLastExistingTile = this.getLastOneXoneExistingTile;
                this._fromCurrentGrid = this._oneXOneConvetor.TryFromTwoXOne;
                this._toCurrentGrid = this._oneXOneConvetor.TryToTwoXOne;
            }
            else
            {
                this._getLastExistingTile = this.GetLastExistingTile;
                this._fromCurrentGrid = tile => tile;
                this._toCurrentGrid = tile => tile;
            }
            this._getTile = this.GetTileInitilaizer;
            if (origin == TileGridOrigin.LOWER_LEFT)
            {
                this._convertOrigin = tile =>
                {
                    tile.FlipY();
                    return tile;
                };
            }
            else
            {
                this._convertOrigin = tile => tile;
            }
        }

        public abstract void Reset();

        public virtual void UpdateMetadata(Data data)
        {
            Console.WriteLine($"{this.type} source, skipping metadata update");
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
            if (this.origin == TileGridOrigin.LOWER_LEFT)
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
                var targetTile = this._convertOrigin(tile);
                targetTile = this._fromCurrentGrid(targetTile);
                return targetTile;
            }).Where(tile => tile != null);
            this.InternalUpdateTiles(targetTiles);
        }

        protected abstract void InternalUpdateTiles(IEnumerable<Tile> targetTiles);

        public virtual void Wrapup()
        {
            Console.WriteLine($"{this.type} source, skipping wrapup phase");
        }

        public abstract bool Exists();

        public abstract int TileCount();

        public abstract void setBatchIdentifier(string batchIdentifier);

        public static Data CreateDatasource(string type, string path, int batchSize, bool isOneXOne, TileGridOrigin? origin = null, bool isBase = false)
        {
            Data data;
            switch (type.ToLower())
            {
                case "gpkg":
                    if (origin == null)
                        data = new Gpkg(path, batchSize, isOneXOne);
                    else
                        data = new Gpkg(path, batchSize, isOneXOne, origin.Value);
                    break;
                case "s3":
                    string s3Url = Configuration.Instance.GetConfiguration("S3", "url");
                    string bucket = Configuration.Instance.GetConfiguration("S3", "bucket");
                    var client = S3.GetClient(s3Url);
                    path = PathUtils.RemoveTrailingSlash(path);
                    if (origin == null)
                        data = new S3(client, bucket, path, batchSize, isOneXOne);
                    else
                        data = new S3(client, bucket, path, batchSize, isOneXOne, origin.Value);
                    break;
                case "fs":
                    if (origin == null)
                        data = new FS(DataType.FOLDER, path, batchSize, isOneXOne, isBase);
                    else
                        data = new FS(DataType.FOLDER, path, batchSize, isOneXOne, isBase, origin.Value);
                    break;
                case "wmts":
                case "xyz":
                case "tms":
                    throw new Exception("web tile source requires extent, and zoom restrictions");
                default:
                    throw new Exception($"Currently there is no support for the data type '{type}'");
            }

            if (!data.Exists())
            {
                //skip existence validation for base data to allow creation of new data for FS and S3
                if (isBase)
                    Console.WriteLine($"base data at path '{path}' does not exists and will be created");
                else
                    throw new Exception($"path '{path}' to data does not exist.");
            }

            return data;
        }

        public static Data CreateDatasource(string type, string path, int batchSize, bool isBase, Extent extent, int maxZoom, int minZoom = 0, bool isOneXone = false, TileGridOrigin? origin = null)
        {
            Data data;
            type = type.ToLower();
            switch (type)
            {
                case "gpkg":
                case "s3":
                case "fs":
                    return CreateDatasource(type, path, batchSize, isOneXone, origin, isBase);
            };
            if (isBase)
            {
                throw new Exception("web tile source cannot be used as base (target) layer");
            }
            switch (type)
            {
                case "wmts":
                    if (origin == null)
                        data = new WMTS(DataType.WMTS, path, batchSize, extent, maxZoom, minZoom, isOneXone);
                    else
                        data = new WMTS(DataType.WMTS, path, batchSize, extent, maxZoom, minZoom, isOneXone, origin.Value);
                    break;
                case "xyz":
                    if (origin == null)
                        data = new XYZ(DataType.XYZ, path, batchSize, extent, maxZoom, minZoom, isOneXone);
                    else
                        data = new XYZ(DataType.XYZ, path, batchSize, extent, maxZoom, minZoom, isOneXone, origin.Value);
                    break;
                case "tms":
                    if (origin == null)
                        data = new TMS(DataType.TMS, path, batchSize, extent, maxZoom, minZoom, isOneXone);
                    else
                        data = new TMS(DataType.TMS, path, batchSize, extent, maxZoom, minZoom, isOneXone, origin.Value);
                    break;
                default:
                    throw new Exception($"Currently there is no support for the data type '{type}'");
            }

            if (!data.Exists())
            {
                //skip existence validation for base data to allow creation of new data for FS and S3
                if (isBase)
                    Console.WriteLine($"base data at path '{path}' does not exists and will be created");
                else
                    throw new Exception($"path '{path}' to data does not exist.");
            }

            return data;
        }
    }
}
