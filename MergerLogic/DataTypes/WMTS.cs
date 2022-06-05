using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class WMTS : HttpDataSource
    {
        public WMTS(DataType type, string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0, bool isOneXOne = false,
            TileGridOrigin tileGridOrigin = TileGridOrigin.UPPER_LEFT)
            : base(type, path, batchSize, extent, tileGridOrigin, maxZoom, minZoom, isOneXOne)
        {
        }
    }
}
