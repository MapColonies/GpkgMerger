using Prometheus;

namespace MergerLogic.Monitoring.Metrics
{
    public interface IMetricsProvider
    {
        void TaskExecutionTimeHistogram(double value, string[]? labelValues = null);
        void BatchInitializationTimeHistogram(double value, string[]? labelValues = null);
        void BatchUploadTimeHistogram(double value, string[]? labelValues = null);
        void BatchWorkTimeHistogram(double value, string[]? labelValues = null);
        void BuildSourcesListTime(double value, string[]? labelValues = null);
        void MergeTimePerTileHistogram(double value, string[]? labelValues = null);
        void UpscaleTimePerTileHistogram(double value, string[]? labelValues = null);
        void TotalValidationTimeHistogram(double value, string[]? labelValues = null);
        void TotalFetchTimePerTileHistogram(double value, string[]? labelValues = null);
        void TilesInBatchGauge(double value, string[]? labelValues = null);
    }
}
