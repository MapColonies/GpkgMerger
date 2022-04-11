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

        protected const int ZOOM_LEVEL_COUNT = 30;

        protected const int COORDS_FOR_ALL_ZOOM_LEVELS = ZOOM_LEVEL_COUNT << 1;

        public Data(DataType type, string path, int batchSize)
        {
            this.type = type;
            this.path = path;
            this.batchSize = batchSize;
        }

        public abstract void UpdateMetadata(Data data);

        protected abstract Tile GetLastExistingTile(Tile tile);

        public abstract List<Tile> GetNextBatch();

        protected abstract List<Tile> CreateCorrespondingBatch(List<Tile> tiles, bool upscale);

        public virtual List<Tile> GetCorrespondingBatch(List<Tile> tiles)
        {
            return CreateCorrespondingBatch(tiles, false);
        }

        public virtual List<Tile> GetUpscaledCorrespondingBatch(List<Tile> tiles)
        {
            return CreateCorrespondingBatch(tiles, true);
        }

        public abstract void UpdateTiles(List<Tile> tiles);

        public abstract void Cleanup();

        public abstract bool Exists();

        public abstract int TileCount();

        public static Data CreateDatasource(string type, string path, int batchSize)
        {
            string s3Url = Configuration.Instance.GetConfiguration("S3", "url");
            string bucket = Configuration.Instance.GetConfiguration("S3", "bucket");

            Data data;
            switch (type.ToLower())
            {
                case "gpkg":
                    data = new Gpkg(path, batchSize);
                    break;
                case "s3":
                    path = PathUtils.RemoveTrailingSlash(path);
                    data = new S3(s3Url, bucket, path, batchSize);
                    break;
                default:
                    throw new Exception($"Currently there is no support for the data type '{type}'");
            }

            return data;
        }
    }
}
