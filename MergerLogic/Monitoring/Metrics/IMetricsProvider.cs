using Prometheus;

namespace MergerLogic.Monitoring.Metrics
{
    public interface IMetricsProvider
    {
        Histogram? TaskExecutionTimeHistogram();
        Histogram? BatchInitializationTimeHistogram();
        Histogram? BatchUploadTimeHistogram();
        Histogram? BatchWorkTimeHistogram();
        Histogram? BuildSourcesListTime();
        Histogram? MergeTimePerTileHistogram();
        Histogram? UpscaleTimePerTileHistogram();
        Histogram? TotalValidationTimeHistogram();
        Histogram? TotalFetchTimePerTileHistogram();
        Gauge? TilesInBatchGauge();
    }
}
