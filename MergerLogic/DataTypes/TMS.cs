using MergerLogic.Batching;
using MergerLogic.Monitoring.Metrics;

namespace MergerLogic.DataTypes
{
    public class TMS : HttpDataSource
    {
        public TMS(IServiceProvider container, IMetricsProvider metricsProvider,
            string path, int batchSize, Extent extent, Grid? grid, GridOrigin? origin, int maxZoom, int minZoom = 0)
            : base(container, metricsProvider, DataType.TMS, path, batchSize, extent, origin, grid, maxZoom, minZoom)
        {
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.LOWER_LEFT;
        }
    }
}
