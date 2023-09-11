
using MergerLogic.Utils;
using Prometheus;
using System.Reflection;

namespace MergerLogic.Monitoring.Metrics
{
    public class MetricsProvider : IMetricsProvider
    {
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

        public Histogram? TaskExecutionTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "task_execution_time",
                "Histogram of task execution times in seconds",
                new string[] { "task_type", "success" },
                this._buckets
            );
        }

        public Histogram? BatchInitializationTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "batch_initialization_time",
                "Histogram of Batch Initialization time",
                null,
                this._buckets
            );
        }

        public Gauge? TilesInBatchGauge()
        {
            return GetOrCreateGauge
            (
                "tiles_in_batch",
                "Number of tiles in a batch",
                new string[] { "target_format" }
            );
        }

        public Histogram? BatchUploadTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "batch_upload_time",
                "Histogram of Batch Target Upload Time",
                new string[] { "target_type" },
                this._buckets
            );
        }

        public Histogram? BuildSourcesListTime()
        {
            return GetOrCreateHistogram
            (
                "build_sources_list_time",
                "Histogram of Build Sources List time",
                null,
                this._buckets
            );
        }

        public Histogram? BatchWorkTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "batch_work_time",
                "Histogram of Batch Work time",
                null,
                this._buckets
            );
        }

        public Histogram? MergeTimePerTileHistogram()
        {
            return GetOrCreateHistogram
            (
                "merge_time_per_tile",
                "Histogram of Merge Time per Tile",
                new string[] { "tile_format" },
                this._buckets
            );
        }

        public Histogram? UpscaleTimePerTileHistogram()
        {
            return GetOrCreateHistogram
            (
                "upscale_time_per_tile",
                "Histogram of Upscale Time per Tile",
               null,
                this._buckets

            );
        }

        public Histogram? TotalFetchTimePerTileHistogram()
        {
            return GetOrCreateHistogram
            (
                "total_fetch_time_per_tile",
                "Histogram of Total Fetch Time per Tile",
               null,
                this._buckets

            );
        }

        public Histogram? TotalValidationTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "total_validation_time",
                "Histogram of Total Validation time",
                null,
                this._buckets

            );
        }

        private Histogram? GetOrCreateHistogram(string metricName, string help, string[]? labels = null, double[]? buckets = null)
        {
            if (!this._enabled)
            {
                return null;
            }

            return Prometheus.Metrics.WithCustomRegistry(_registry).CreateHistogram(metricName, help,
                new HistogramConfiguration
                {
                    Buckets = buckets,
                    LabelNames = labels,
                });
        }

        private Gauge? GetOrCreateGauge(string metricName, string help, string[]? labels = null)
        {
            if (!this._enabled)
            {
                return null;
            }
            return Prometheus.Metrics.WithCustomRegistry(_registry).CreateGauge(metricName, help,
                new GaugeConfiguration
                {
                    LabelNames = labels
                });
        }


    }
}
