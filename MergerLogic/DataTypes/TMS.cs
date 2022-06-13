using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class TMS : HttpDataSource
    {
        public TMS(IServiceProvider container,
            string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false, GridOrigin origin = GridOrigin.LOWER_LEFT)
            : base(container, DataType.TMS, path, batchSize, extent, origin, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
