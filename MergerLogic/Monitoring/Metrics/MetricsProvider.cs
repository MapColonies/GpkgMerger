using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using Prometheus;
using System.Reflection;
using System.Runtime.Serialization;

namespace MergerLogic.Monitoring.Metrics
{
    public class MetricsProvider : IMetricsProvider
    {

        private enum MetricName
        {
            [EnumMember(Value = "task_execution_time")] TaskExecutionTimeHistogram,
            [EnumMember(Value = "batch_upload_time")] BatchUploadTimeHistogram,
            [EnumMember(Value = "batch_work_time")] BatchWorkTimeHistogram,
            [EnumMember(Value = "build_sources_list_time")] BuildSourcesListTimeHistogram,
            [EnumMember(Value = "merge_time_per_tile")] MergeTimePerTileHistogram,
            [EnumMember(Value = "upscale_time_per_tile")] UpscaleTimePerTileHistogram,
            [EnumMember(Value = "total_validation_time")] TotalValidationTimeHistogram,
            [EnumMember(Value = "total_fetch_time_per_tile")] TotalFetchTimePerTileHistogram,
            [EnumMember(Value = "tiles_in_batch")] TilesInBatchGauge,
        }
        private readonly CollectorRegistry _registry;
        private readonly double[]? _buckets;
        private readonly bool _enabled;

        public MetricsProvider(IConfigurationManager configurationManager)
        {
            var appInfo = Assembly.GetEntryAssembly()?.GetName();
            string appName = appInfo?.Name ?? "MergerService";
            this._registry = Prometheus.Metrics.DefaultRegistry;
            this._registry.SetStaticLabels(new Dictionary<string, string>() { { "app", appName } });
            this._buckets = configurationManager.GetConfiguration<double[]>("METRICS", "measurementBuckets");
            this._enabled = configurationManager.GetConfiguration<bool>("METRICS", "enabled");
        }

        public void TaskExecutionTimeHistogram(double measuredTime, string taskType)
        {
            string[] labelValues = new string[] { taskType };

            this.ObserveHistogram
            (
                MetricName.TaskExecutionTimeHistogram,
                "Histogram of task execution times in seconds",
                measuredTime,
                new string[] { "task_type" },
                labelValues
           );
        }

        public void BatchUploadTimeHistogram(double measuredTime, DataType targetType)
        {
            string[] labelValues = new string[] { targetType.ToString() };

            this.ObserveHistogram
           (
               MetricName.BatchUploadTimeHistogram,
               "Histogram of Batch Target Upload Time",
               measuredTime,
               new string[] { "target_type" },
               labelValues
           );
        }

        public void BuildSourcesListTime(double measuredTime)
        {
            this.ObserveHistogram
           (
               MetricName.BuildSourcesListTimeHistogram,
               "Histogram of Build Sources List time",
               measuredTime,
               null,
               null
           );
        }

        public void BatchWorkTimeHistogram(double measuredTime)
        {
            this.ObserveHistogram
           (
               MetricName.BatchWorkTimeHistogram,
               "Histogram of Batch Work time",
               measuredTime,
               null,
               null
           );
        }

        public void MergeTimePerTileHistogram(double measuredTime, TileFormat tileFormat)
        {
            string[] labelValues = new string[] { tileFormat.ToString() };

            this.ObserveHistogram
           (
               MetricName.MergeTimePerTileHistogram,
               "Histogram of Merge Time per Tile",
               measuredTime,
               new string[] { "tile_format" },
               labelValues
           );
        }

        public void UpscaleTimePerTileHistogram(double measuredTime)
        {
            this.ObserveHistogram
           (
              MetricName.UpscaleTimePerTileHistogram,
               "Histogram of Upscale Time per Tile",
               measuredTime,
               null,
               null
           );
        }

        public void TotalFetchTimePerTileHistogram(double measuredTime)
        {
            this.ObserveHistogram
           (
               MetricName.TotalFetchTimePerTileHistogram,
               "Histogram of Total Fetch Time per Tile",
               measuredTime,
               null,
               null
           );
        }

        public void TotalValidationTimeHistogram(double measuredTime)
        {
            this.ObserveHistogram
            (
                MetricName.TotalValidationTimeHistogram,
                "Histogram of Total Validation time",
                measuredTime,
                null,
                null
            );
        }

        public void TilesInBatchGauge(double batchCount)
        {
            this.SetGauge
           (
               MetricName.TilesInBatchGauge,
               "Number of tiles in a batch",
               batchCount,
               null,
               null
           );
        }

        private void ObserveHistogram(MetricName metricName, string help, double value, string[]? labels = null, string[]? labelValues = null)
        {
            if (!this._enabled)
            {
                return;
            }

            Histogram histogram = Prometheus.Metrics.WithCustomRegistry(_registry).CreateHistogram(metricName.ToString(), help,
            new HistogramConfiguration
            {
                Buckets = this._buckets,
                LabelNames = labels,
            });

            if (labelValues != null)
            {
                histogram.WithLabels(labelValues);
            }
            histogram.Observe(value);
        }


        private void SetGauge(MetricName metricName, string help, double value, string[]? labels = null, string[]? labelValues = null)
        {
            if (!this._enabled)
            {
                return;

            }

            Gauge gauge = Prometheus.Metrics.WithCustomRegistry(_registry).CreateGauge(metricName.ToString(), help,
                new GaugeConfiguration
                {
                    LabelNames = labels
                });

            if (labelValues != null)
            {
                gauge.WithLabels(labelValues);
            }
            gauge.Set(value);
        }
    }
}
