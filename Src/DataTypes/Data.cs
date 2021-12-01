using System.Collections.Generic;
using GpkgMerger.Src.Batching;

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

        public abstract void UpdateMetadata(Data data);

        public abstract List<Tile> GetNextBatch();

        public abstract List<Tile> GetCorrespondingBatch(List<Tile> tiles);

        public abstract void UpdateTiles(List<Tile> tiles);

        public abstract void Cleanup();
    }
}