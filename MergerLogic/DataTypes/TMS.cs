using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class TMS : HttpDataSource
    {
        public TMS(IUtilsFactory utilsFactory, IOneXOneConvetor oneXOneConvetor,
            string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false, GridOrigin origin = GridOrigin.LOWER_LEFT)
            : base(utilsFactory, oneXOneConvetor, DataType.TMS, path, batchSize, extent, origin, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
