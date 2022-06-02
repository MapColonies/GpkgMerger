using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class XYZ : HttpDataSource
    {
        public XYZ(DataType type, string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false,
            TileGridOrigin origin = TileGridOrigin.UPPER_LEFT)
            : base(type, path, batchSize, extent, origin, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
