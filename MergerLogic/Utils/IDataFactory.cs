using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IDataFactory
    {
        IData CreateDatasource(string type, string path, int batchSize, bool isOneXOne, GridOrigin? origin = null, bool isBase = false);
        IData CreateDatasource(string type, string path, int batchSize, bool isBase, Extent extent, int maxZoom, int minZoom = 0, bool isOneXone = false, GridOrigin? origin = null);
    }
}
