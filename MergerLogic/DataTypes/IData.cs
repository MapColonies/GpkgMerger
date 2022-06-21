using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public interface IData
    {
        public DataType Type { get; }
        public string Path { get; }

        bool Exists();
        Tile GetCorrespondingTile(Coord coords, bool upscale);
        List<Tile> GetNextBatch(out string batchIdentifier);
        void Reset();
        void setBatchIdentifier(string batchIdentifier);
        int TileCount();
        bool TileExists(Coord coord);
        bool TileExists(Tile tile);
        void UpdateMetadata(IData data);
        void UpdateTiles(IEnumerable<Tile> tiles);
        void Wrapup();
    }
}
