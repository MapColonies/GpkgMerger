using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class WMTS : HttpDataSource
    {
        public WMTS(IUtilsFactory utilsFactory, IOneXOneConvetor oneXOneConvetor,
            string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false, GridOrigin tileGridOrigin = GridOrigin.UPPER_LEFT)
            : base(utilsFactory, oneXOneConvetor, DataType.WMTS, path, batchSize, extent, tileGridOrigin, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
