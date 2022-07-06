using MergerLogic.Batching;

namespace MergerLogic.Utils
{
    public interface IS3Utils : IDataUtils
    {
        void UpdateTile(Tile tile);
        Tile GetTile(string key);
    }
}
