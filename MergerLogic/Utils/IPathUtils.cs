using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;

namespace MergerLogic.Utils
{
    public interface IPathUtils
    {
        Coord FromPath(string path, bool isS3 = false);
        string GetTilePath(string basePath, int z, int x, int y, TileFormat format, bool isS3 = false);
        string GetTilePath(string basePath, Tile tile, bool isS3 = false);
        string RemoveTrailingSlash(string path, bool isS3 = false);
        string GetTilePathWithoutExtension(string basePath, int z, int x, int y, bool isS3 = false);
    }
}
