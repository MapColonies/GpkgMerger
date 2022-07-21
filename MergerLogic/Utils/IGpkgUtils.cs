using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IGpkgUtils : IDataUtils
    {
        void CreateTileIndex();
        List<Tile> GetBatch(int batchSize, long offset);
        Extent GetExtent();
        Tile GetLastTile(int[] coords, Coord baseCoords);
        long GetTileCount();
        void InsertTiles(IEnumerable<Tile> tiles);
        void UpdateExtent(Extent extent);
        void Vacuum();
        public bool Exist();
        public void Create(Extent extent, bool isOneXOne = false);
        public void DeleteTileTableTriggers();
        public void CreateTileCacheValidationTriggers();
        public void UpdateTileMatrixTable(bool isOneXOne = false);
        public bool IsValidGrid(bool isOneXOne = false);
    }
}
