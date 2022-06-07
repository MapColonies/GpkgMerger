using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IDataFactory
    {
        Data CreateDatasource(string type, string path, int batchSize, bool isOneXOne, TileGridOrigin? origin = null, bool isBase = false);
        Data CreateDatasource(string type, string path, int batchSize, bool isBase, Extent extent, int maxZoom, int minZoom = 0, bool isOneXone = false, TileGridOrigin? origin = null);
    }
}