using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class WMTS : HttpDataSource
    {
        public WMTS(IServiceProvider container,
            string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false, GridOrigin tileGridOrigin = GridOrigin.UPPER_LEFT)
            : base(container, DataType.WMTS, path, batchSize, extent, tileGridOrigin, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
