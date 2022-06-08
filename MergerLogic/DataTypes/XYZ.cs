using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class XYZ : HttpDataSource
    {
        public XYZ(IUtilsFactory utilsFactory, IOneXOneConvetor oneXOneConvetor,
            string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false, GridOrigin origin = GridOrigin.UPPER_LEFT)
            : base(utilsFactory, oneXOneConvetor, DataType.XYZ, path, batchSize, extent, origin, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
