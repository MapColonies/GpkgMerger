using Amazon.S3.Model;
using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.Clients
{
    public interface IS3Client : IDataUtils
    {
        void UpdateTile(Tile tile);
        Tile? GetTile(string key);
        string Bucket { get; }
        ListObjectsV2Response ListObject(ref string? continuationToken, string prefix, string startAfter, int? maxKeys = null);
    }
}
