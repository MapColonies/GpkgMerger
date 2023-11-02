using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IDataFactory
    {
        IData CreateDataSource(string type, string path, string bucket, int batchSize, Grid? grid = null, GridOrigin? origin = null, Extent? extent = null, bool isBase = false);
        IData CreateDataSource(string type, string path, string bucket, int batchSize, bool isBase, Extent extent, int maxZoom, int minZoom = 0, Grid? grid = null, GridOrigin? origin = null);
    }
}
