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
        protected delegate Tile GetTileFunction(int z, int x, int y);

        public readonly DataType type;
        public readonly string path;
        public readonly bool IsOneXOne;
        protected readonly int batchSize;
        protected DataUtils utils;
        protected GetTileFunction _getTile;
        protected OneXOneConvetor _oneXOneConvetor;

        protected const int ZOOM_LEVEL_COUNT = 30;

        protected const int COORDS_FOR_ALL_ZOOM_LEVELS = ZOOM_LEVEL_COUNT << 1;

        public Data(DataType type, string path, int batchSize, DataUtils utils, bool isOneXOne = false)
        {
            this.type = type;
            this.path = path;
            this.batchSize = batchSize;
            this.utils = utils;
            this.IsOneXOne = isOneXOne;
            _oneXOneConvetor = new OneXOneConvetor();
            this._getTile = this.GetTileInitilaizer;
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

                lastTile = this._getTile(i, baseTileX, baseTileY);
                if (lastTile != null)
                {
                    break;
                }
            }

            return lastTile;
        }

        protected virtual Tile GetOneXOneTile(int z, int x, int y)
        {
            Coord coords = this._oneXOneConvetor.TryFromTwoXOne(z, x, y);
            if (coords == null)
            {
                return null;
            }
            return this.utils.GetTile(coords);
        }

        //lazy load get tile function on first call for compatibility with null until in contractor
        protected Tile GetTileInitilaizer(int z, int x, int y)
        {
            this._getTile = this.IsOneXOne ? this.GetOneXOneTile : this.utils.GetTile;
            return this._getTile(z, x, y);
        }

        public abstract List<Tile> GetNextBatch(out string batchIdentifier);

        public Tile GetCorrespondingTile(Coord coords, bool upscale)
        {
            Tile correspondingTile = this._getTile(coords.z, coords.x, coords.y);

            if (upscale && correspondingTile == null)
            {
                correspondingTile = this.GetLastExistingTile(coords);
            }
            return correspondingTile;
        }

        public abstract void UpdateTiles(List<Tile> tiles);

        public virtual void Wrapup()
        {
            Console.WriteLine($"{this.type} source, skipping wrapup phase");
        }

        public abstract bool Exists();

        public abstract int TileCount();

        public abstract void setBatchIdentifier(string batchIdentifier);

        public static Data CreateDatasource(string type, string path, int batchSize, bool isBase = false)
        {
            Data data;
            switch (type.ToLower())
            {
                case "gpkg":
                    data = new Gpkg(path, batchSize);
                    break;
                case "s3":
                    string s3Url = Configuration.Instance.GetConfiguration("S3", "url");
                    string bucket = Configuration.Instance.GetConfiguration("S3", "bucket");

                    var client = S3.GetClient(s3Url);
                    path = PathUtils.RemoveTrailingSlash(path);
                    data = new S3(client, bucket, path, batchSize);
                    break;
                case "fs":
                    data = new FS(DataType.FOLDER, path, batchSize, isBase);
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

        public static Data CreateDatasource(string type, string path, int batchSize, bool isBase, Extent extent, int maxZoom, int minZoom = 0)
        {
            Data data;
            type = type.ToLower();
            switch (type)
            {
                case "gpkg":
                case "s3":
                case "fs":
                    return CreateDatasource(type, path, batchSize, isBase);
            };
            if (isBase)
            {
                throw new Exception("web tile source cannot be used as base (target) layer");
            }
            switch (type)
            {
                case "wmts":
                    data = new WMTS(DataType.WMTS, path, batchSize, extent, maxZoom, minZoom);
                    break;
                case "xyz":
                    data = new XYZ(DataType.XYZ, path, batchSize, extent, maxZoom, minZoom);
                    break;
                case "tms":
                    data = new TMS(DataType.TMS, path, batchSize, extent, maxZoom, minZoom);
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
