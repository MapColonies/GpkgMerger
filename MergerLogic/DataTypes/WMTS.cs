using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class WMTS : HttpDataSource
    {
        public WMTS(IServiceProvider container,
            string path, int batchSize, Extent extent, Grid? grid, GridOrigin? tileGridOrigin, int maxZoom, int minZoom = 0)
            : base(container, DataType.WMTS, path, batchSize, extent, tileGridOrigin, grid, maxZoom, minZoom)
        {
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.UPPER_LEFT;
        }
    }
}
