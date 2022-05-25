using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class WMTS : HttpDataSource
    {
        public WMTS(DataType type, string path, int batchSize, Extent extent, int maxZoom, int minZoom = 0) 
            : base(type, path, batchSize, extent, TileGridOrigin.UPPER_LEFT, maxZoom, minZoom)
        {
        }
    }
}
