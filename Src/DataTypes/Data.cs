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
        public DataType type;
        public string path;

        public Data(DataType type, string path)
        {
            this.type = type;
            this.path = path;
        }

        public abstract void UpdateMetadata(Data data);

        public abstract List<Tile> GetNextBatch();

        public abstract List<Tile> GetCorrespondingBatch(List<Tile> tiles);

        public abstract void UpdateTiles(List<Tile> tiles);

        public abstract void Cleanup();

        public abstract bool Exists();

        public static Data CreateDatasource(string type, string path)
        {
            string s3Url = Configuration.Instance.GetConfiguration("S3", "url");
            string bucket = Configuration.Instance.GetConfiguration("S3", "bucket");

            Data data;
            switch (type.ToLower())
            {
                case "gpkg":
                    data = new Gpkg(path, 1000);
                    break;
                case "s3":
                    data = new S3(s3Url, bucket, path);
                    break;
                default:
                    return null;
            }

            return data;
        }
    }
}