using System;
using System.Collections.Generic;
using GpkgMerger.Src.Batching;
using GpkgMerger.Src.Utils;

namespace GpkgMerger.Src.DataTypes
{
    public enum DataType
    {
        GPKG,
        FOLDER,
        S3
    }

    public abstract class Data
    {
        public readonly DataType type;
        protected readonly string path;
        protected readonly int batchSize;
        protected DataUtils utils;

        protected const int ZOOM_LEVEL_COUNT = 30;

        protected const int COORDS_FOR_ALL_ZOOM_LEVELS = ZOOM_LEVEL_COUNT << 1;

        public Data(DataType type, string path, int batchSize, DataUtils utils)
        {
            this.type = type;
            this.path = path;
            this.batchSize = batchSize;
            this.utils = utils;
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

        public abstract List<Tile> GetNextBatch();

        public Tile GetCorrespondingTile(Coord coords, bool upscale)
        {
            Tile correspondingTile = this.utils.GetTile(coords);

            if (upscale && correspondingTile == null)
            {
                correspondingTile = GetLastExistingTile(coords);
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
                    data = new FS(DataType.FOLDER, path, batchSize);
                    break;
                default:
                    throw new Exception($"Currently there is no support for the data type '{type}'");
            }

            if (!data.Exists())
            {
                //skip exsistence validation for base data to allow creation of new data for FS and S3
                if (isBase)
                    Console.WriteLine($"base data at path '{path}' does not exists and will be created");
                else
                    throw new Exception($"path '{path}' to data does not exist.");
            }

            return data;
        }
    }
}
