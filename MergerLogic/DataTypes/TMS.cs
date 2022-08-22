using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class TMS : HttpDataSource
    {
        public TMS(IServiceProvider container,
            string path, int batchSize, Extent extent, Grid? grid, GridOrigin? origin, int maxZoom, int minZoom = 0)
            : base(container, DataType.TMS, path, batchSize, extent, origin, grid, maxZoom, minZoom)
        {
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.LOWER_LEFT;
        }
    }
}
