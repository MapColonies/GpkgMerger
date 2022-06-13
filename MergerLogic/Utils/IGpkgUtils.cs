using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IGpkgUtils : IDataUtils
    {
        void CreateTileIndex();
        List<Tile> GetBatch(int batchSize, int offset);
        Extent GetExtent();
        Tile GetLastTile(int[] coords, Coord baseCoords);
        string GetTileCache();
        int GetTileCount();
        void InsertTiles(IEnumerable<Tile> tiles);
        void UpdateExtent(Extent extent);
        void Vacuum();
        public bool Exist();
        public void Create(Extent extent, int maxZoom);
        public void RemoveUnusedTileMatrix(IEnumerable<int> usedZooms);
    }
}
