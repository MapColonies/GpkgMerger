using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class XYZ : HttpDataSource
    {
        public XYZ(IServiceProvider container,
            string path, int batchSize, Extent extent, bool? isOneXOne, GridOrigin? origin, int maxZoom, int minZoom = 0)
            : base(container, DataType.XYZ, path, batchSize, extent, origin, isOneXOne, maxZoom, minZoom)
        {
        }
    }
}
