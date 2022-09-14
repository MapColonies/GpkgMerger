using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.Clients
{
    public interface IS3Client : IDataUtils
    {
        void UpdateTile(Tile tile);
        Tile? GetTile(string key);
    }
}
