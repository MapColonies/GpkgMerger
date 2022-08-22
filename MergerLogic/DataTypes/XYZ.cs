using MergerLogic.Batching;

namespace MergerLogic.DataTypes
{
    public class XYZ : HttpDataSource
    {
        public XYZ(IServiceProvider container,
            string path, int batchSize, Extent extent, Grid? grid, GridOrigin? origin, int maxZoom, int minZoom = 0)
            : base(container, DataType.XYZ, path, batchSize, extent, origin, grid, maxZoom, minZoom)
        {
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.UPPER_LEFT;
        }
    }
}
