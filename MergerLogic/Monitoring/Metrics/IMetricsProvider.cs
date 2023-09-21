using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using Prometheus;

namespace MergerLogic.Monitoring.Metrics
{
    public interface IMetricsProvider
    {
        void TaskExecutionTimeHistogram(double measuredTime, string taskType);
        void BatchInitializationTimeHistogram(double measuredTime);
        void BatchUploadTimeHistogram(double measuredTime, DataType targetType);
        void BuildSourcesListTime(double measuredTime);
        void BatchWorkTimeHistogram(double measuredTime);
        void MergeTimePerTileHistogram(double measuredTime, TileFormat tileFormat);
        void TotalFetchTimePerTileHistogram(double measuredTime);
        void UpscaleTimePerTileHistogram(double measuredTime);
        void TotalValidationTimeHistogram(double measuredTime);
        void TilesInBatchGauge(double batchCount);
    }
}
