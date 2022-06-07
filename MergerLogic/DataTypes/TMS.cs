using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class TMS : HttpDataSource
    {
        public TMS(DataType type, string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false,
            GridOrigin origin = GridOrigin.LOWER_LEFT)
            : base(type, path, batchSize, extent, origin, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
