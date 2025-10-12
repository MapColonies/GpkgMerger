using MergerLogic.Batching;
using MergerLogic.ImageProcessing;

namespace MergerLogic.DataTypes
{
    public interface IData
    {
        public DataType Type { get; }
        public string Path { get; }
        public bool IsNew { get; set; }

        bool Exists();
        Tile? GetCorrespondingTile(Coord coords, TileFormat? format, bool upscale);
        List<Tile> GetNextBatch(out string batchIdentifier, out string? nextBatchIdentifier, long? totalTilesCount);
        void Reset();
        void setBatchIdentifier(string batchIdentifier);
        long TileCount();
        bool TileExists(Coord coord, TileFormat? format);
        bool TileExists(Tile tile, TileFormat? format);
        void UpdateTiles(IEnumerable<Tile> tiles);
        void Wrapup();
    }
}
