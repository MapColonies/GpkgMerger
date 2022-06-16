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
        public void Create(Extent extent, bool isOneXOne = false);
        public void RemoveUnusedTileMatrix(IEnumerable<int> usedZooms);
        public void DeleteTileTableTriggers();
        public void CreateTileCacheValidationTriggers();
        public void UpdateTileMatrixTable(bool isOneXOne = false);
    }
}
