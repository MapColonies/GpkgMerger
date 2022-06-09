using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IGpkgUtils : IDataUtils
    {
        void CreateTileIndex(string tileCache);
        List<Tile> GetBatch(int batchSize, int offset, string tileCache);
        Extent GetExtent();
        Tile GetLastTile(string tileCache, int[] coords, Coord baseCoords);
        string GetTileCache();
        int GetTileCount(string tileCache);
        void InsertTiles(string tileCache, IEnumerable<Tile> tiles);
        void UpdateExtent(Extent extent);
        void Vacuum();
    }
}
