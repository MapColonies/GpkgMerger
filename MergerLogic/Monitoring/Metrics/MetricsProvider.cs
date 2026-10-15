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
            [EnumMember(Value = "merge_tile_outcomes_total")] MergeTileOutcomesCounter,
            [EnumMember(Value = "merger_task_outcomes_total")] TaskOutcomesCounter,
            [EnumMember(Value = "merger_report_write_failures_total")] ReportWriteFailuresCounter,
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

        public void MergeTileOutcomes(int added, int merged, int replaced, int skipped, string taskType, string targetFormat, bool isNewTarget)
        {
            const string help = "Count of merged tiles by outcome (added / merged / replaced / skipped)";
            string[] labels = new string[] { "outcome", "task_type", "target_format", "is_new_target" };
            string newTarget = isNewTarget ? "true" : "false";

            this.IncrementCounter(MetricName.MergeTileOutcomesCounter, help, added, labels,
                new string[] { "added", taskType, targetFormat, newTarget });
            this.IncrementCounter(MetricName.MergeTileOutcomesCounter, help, merged, labels,
                new string[] { "merged", taskType, targetFormat, newTarget });
            this.IncrementCounter(MetricName.MergeTileOutcomesCounter, help, replaced, labels,
                new string[] { "replaced", taskType, targetFormat, newTarget });
            this.IncrementCounter(MetricName.MergeTileOutcomesCounter, help, skipped, labels,
                new string[] { "skipped", taskType, targetFormat, newTarget });
        }

        public void TaskOutcome(string result, string taskType)
        {
            this.IncrementCounter(MetricName.TaskOutcomesCounter, "Count of task outcomes by result (success / reject / error)",
                1, new string[] { "result", "task_type" }, new string[] { result, taskType });
        }

        public void ReportWriteFailure()
        {
            this.IncrementCounter(MetricName.ReportWriteFailuresCounter, "Count of merge-report artifact write failures", 1);
        }

        // The Prometheus metric name is the MetricName's [EnumMember] value (snake_case), not the
        // enum member identifier — dashboards and Prometheus naming conventions expect snake_case.
        private static string MetricNameValue(MetricName metricName)
        {
            return typeof(MetricName).GetField(metricName.ToString())!
                .GetCustomAttribute<EnumMemberAttribute>()!.Value!;
        }

        private void IncrementCounter(MetricName metricName, string help, double value, string[]? labels = null, string[]? labelValues = null)
        {
            if (!this._enabled)
            {
                return;
            }

            Counter counter = Prometheus.Metrics.WithCustomRegistry(_registry).CreateCounter(MetricNameValue(metricName), help,
                new CounterConfiguration { LabelNames = labels });

            if (labelValues != null)
            {
                counter.WithLabels(labelValues).Inc(value);
            }
            else
            {
                counter.Inc(value);
            }
        }

        private void ObserveHistogram(MetricName metricName, string help, double value, string[]? labels = null, string[]? labelValues = null)
        {
            if (!this._enabled)
            {
                return;
            }

            Histogram histogram = Prometheus.Metrics.WithCustomRegistry(_registry).CreateHistogram(MetricNameValue(metricName), help,
            new HistogramConfiguration
            {
                Buckets = this._buckets,
                LabelNames = labels,
            });

            if (labelValues != null)
            {
                histogram.WithLabels(labelValues).Observe(value);
            }
            else
            {
                histogram.Observe(value);
            }
        }


        private void SetGauge(MetricName metricName, string help, double value, string[]? labels = null, string[]? labelValues = null)
        {
            if (!this._enabled)
            {
                return;

            }

            Gauge gauge = Prometheus.Metrics.WithCustomRegistry(_registry).CreateGauge(MetricNameValue(metricName), help,
                new GaugeConfiguration
                {
                    LabelNames = labels
                });

            if (labelValues != null)
            {
                gauge.WithLabels(labelValues).Set(value);
            }
            else
            {
                gauge.Set(value);
            }
        }
    }
}
