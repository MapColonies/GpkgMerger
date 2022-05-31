using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class TMS : HttpDataSource
    {
        public TMS(DataType type, string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false) 
            : base(type, path, batchSize, extent, TileGridOrigin.LOWER_LEFT, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
