using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IPathUtils
    {
        Coord FromPath(string path, bool isS3 = false);
        string GetTilePath(string basePath, int z, int x, int y, bool isS3 = false);
        string GetTilePath(string basePath, Tile tile);
        string RemoveTrailingSlash(string path, bool isS3 = false);
    }
}
