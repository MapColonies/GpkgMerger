using Prometheus;

namespace MergerLogic.Monitoring.Metrics
{
    public interface IMetricsProvider
    {
        Histogram TaskExecutionTimeHistogram();
        Histogram TaskInitializationTimeHistogram();
        Histogram TileUploadTimeHistogram();
        Histogram TotalBatchWorkTimeHistogram();
        Histogram TotalGetTilesSourcesTimeHistogram();
        Histogram SourceTileDownloadTimeHistogram();
        Histogram TotalTileMergeTimeHistogram();
        Histogram TotalTileUpscaleTimeHistogram();
        Histogram TotalValidationTimeHistogram();
        Gauge TilesInBatchGauge();
    }
}
