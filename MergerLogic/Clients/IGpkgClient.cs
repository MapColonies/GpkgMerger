using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;

namespace MergerLogic.Clients
{
    public interface IGpkgClient : IDataUtils
    {
        List<Tile> GetBatch(int batchSize, long offset);
        Extent GetExtent();
        Tile GetLastTile(int[] coords, int currentTileZoom);
        long GetTileCount();
        void InsertTiles(IEnumerable<Tile> tiles);
        void UpdateExtent(Extent extent);
        void Vacuum();
        public bool Exist();
        public void Create(Extent extent, bool isOneXOne = false);
        public void DeleteTileTableTriggers();
        public void CreateTileCacheValidationTriggers();
        public void UpdateTileMatrixTable(bool isOneXOne = false);
    }
}
