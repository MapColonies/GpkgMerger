
using MergerLogic.Utils;
using Prometheus;
using System.Reflection;

namespace MergerLogic.Monitoring.Metrics
{
    public class MetricsProvider : IMetricsProvider
    {
        private readonly CollectorRegistry _registry;
        private readonly double[] _buckets;
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

        public Histogram TaskExecutionTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "task_execution_time",
                "Histogram of task execution times in seconds",
                new string[] { "task_type", "success" },
                this._buckets
            );
        }

        public Histogram TaskInitializationTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "task_initialization_time",
                "Histogram of Task Initialization time",
                null,
                this._buckets
            );
        }

        public Gauge TilesInBatchGauge()
        {
            return GetOrCreateGauge
            (
                "tiles_in_batch",
                "Number of tiles in a batch",
                new string[] { "target_format" }
            );
        }

        public Histogram TileUploadTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "tile_upload_time",
                "Histogram of Tile Target Upload Time",
                new string[] { "target_type" },
                this._buckets
            );
        }

        public Histogram TotalGetTilesSourcesTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "get_tiles_sources_time",
                "Histogram of Get Tiles Sources time",
                null,
                this._buckets
            );
        }

        public Histogram SourceTileDownloadTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "source_tile_download_time",
                "Histogram of Source Tile Download time",
                new string[] { "source_type" },
                this._buckets
            );
        }


        public Histogram TotalBatchWorkTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "total_batch_work_time",
                "Histogram of Total Batch Work time",
                null,
                this._buckets
            );
        }

        public Histogram TotalTileMergeTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "total_tile_merge_time",
                "Histogram of Total Tile Merge time",
                new string[] { "tile_format" },
                this._buckets
            );
        }

        public Histogram TotalTileUpscaleTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "total_tile_upscale_time",
                "Histogram of Total Tile Upscale time",
               null,
                this._buckets

            );
        }

        public Histogram TotalValidationTimeHistogram()
        {
            return GetOrCreateHistogram
            (
                "total_validation_time",
                "Histogram of Total Validation time",
                null,
                this._buckets

            );
        }

        private Histogram GetOrCreateHistogram(string metricName, string help, string[] labels = null, double[] buckets = null)
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

        private Gauge GetOrCreateGauge(string metricName, string help, string[] labels = null)
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
