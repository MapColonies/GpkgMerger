using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class XYZ : HttpDataSource
    {
        public XYZ(IServiceProvider container,
            string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false, GridOrigin origin = GridOrigin.UPPER_LEFT)
            : base(container, DataType.XYZ, path, batchSize, extent, origin, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
