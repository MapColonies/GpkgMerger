using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class XYZ : HttpDataSource
    {
        public XYZ(DataType type, string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false)
            : base(type, path, batchSize, extent, TileGridOrigin.UPPER_LEFT, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
