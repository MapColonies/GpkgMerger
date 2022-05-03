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

        public virtual void UpdateMetadata(Data data)
        {
            Console.WriteLine($"{this.type} source, skipping metadata update");
        }

        protected virtual Tile GetLastExistingTile(Tile tile)
        {
            int z = tile.Z;
            int baseTileX = tile.X;
            int baseTileY = tile.Y;

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

        protected List<Tile> CreateCorrespondingBatch(List<Tile> tiles, bool upscale)
        {
            List<Tile> correspondingTiles = new List<Tile>(this.batchSize);

            foreach (Tile tile in tiles)
            {
                Tile correspondingTile = this.utils.GetTile(tile.GetCoord());

                if (upscale && correspondingTile == null)
                {
                    correspondingTile = GetLastExistingTile(tile);
                }

                correspondingTiles.Add(correspondingTile);
            }

            return correspondingTiles;
        }

        public virtual List<Tile> GetCorrespondingBatch(List<Tile> tiles)
        {
            return CreateCorrespondingBatch(tiles, false);
        }

        public virtual List<Tile> GetUpscaledCorrespondingBatch(List<Tile> tiles)
        {
            return CreateCorrespondingBatch(tiles, true);
        }

        public abstract void UpdateTiles(List<Tile> tiles);

        public virtual void Cleanup()
        {
            Console.WriteLine($"{this.type} source, skipping cleanup phase");
        }

        public abstract bool Exists();

        public abstract int TileCount();

        public static Data CreateDatasource(string type, string path, int batchSize)
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
                throw new Exception($"path '{path}' to data does not exist.");
            }

            return data;
        }
    }
}
