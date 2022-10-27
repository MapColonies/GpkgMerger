using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.Clients
{
    public interface IS3Client : IDataUtils
    {
        void UpdateTile(Tile tile);
        Tile? GetTile(string key);
        Task<Tile?>[] GetImages(List<S3Object> s3Objects);
        void UpdateTiles(IEnumerable<Tile> tiles);
    }
}
