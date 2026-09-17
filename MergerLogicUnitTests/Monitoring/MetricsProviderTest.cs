using MergerLogic.Monitoring.Metrics;
using MergerLogic.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prometheus;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MergerLogicUnitTests.Monitoring
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("metrics")]
    public class MetricsProviderTest
    {
        // MetricsProvider writes to the static DefaultRegistry and calls SetStaticLabels in its
        // constructor (allowed once per registry), so a single instance is shared across tests.
        private static MetricsProvider _metricsProvider;

        private static readonly double[] Buckets = new double[] { 0.1, 1, 10 };

        [ClassInitialize]
        public static void BeforeAll(TestContext _)
        {
            var configMock = new Mock<IConfigurationManager>(MockBehavior.Strict);
            configMock.Setup(c => c.GetConfiguration<double[]>("METRICS", "measurementBuckets")).Returns(Buckets);
            configMock.Setup(c => c.GetConfiguration<bool>("METRICS", "enabled")).Returns(true);

            _metricsProvider = new MetricsProvider(configMock.Object);
        }

        private static async Task<string> ScrapeAsync()
        {
            using var stream = new MemoryStream();
            await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        [TestMethod]
        public async Task LabeledHistogram_UsesSnakeCaseNameAndRecordsUnderLabel()
        {
            _metricsProvider.TaskExecutionTimeHistogram(0.5, "MERGE");

            string scrape = await ScrapeAsync();

            Assert.IsTrue(scrape.Contains("task_execution_time"), "snake_case metric name missing");
            Assert.IsFalse(scrape.Contains("TaskExecutionTimeHistogram"),
                "metric must be exposed under its snake_case EnumMember name, not the enum member identifier");
            // the observation must land on the labeled series
            Assert.IsTrue(Regex.IsMatch(scrape, "task_execution_time_count\\{[^}]*task_type=\"MERGE\"[^}]*\\} 1"),
                "labeled count sample not recorded under task_type=MERGE");
        }

        [TestMethod]
        public async Task Gauge_UsesSnakeCaseName()
        {
            _metricsProvider.TilesInBatchGauge(42);

            string scrape = await ScrapeAsync();

            Assert.IsTrue(scrape.Contains("tiles_in_batch"), "snake_case gauge name missing");
            Assert.IsFalse(scrape.Contains("TilesInBatchGauge"));
        }

        [TestMethod]
        public async Task MergeTileOutcomes_ExposesCounterPerOutcome()
        {
            _metricsProvider.MergeTileOutcomes(added: 10, merged: 5, replaced: 2, skipped: 1,
                taskType: "MERGE", targetFormat: "PNG", isNewTarget: false);

            string scrape = await ScrapeAsync();

            Assert.IsTrue(scrape.Contains("merge_tile_outcomes_total"), "counter name missing");
            Assert.IsTrue(Regex.IsMatch(scrape, "merge_tile_outcomes_total\\{[^}]*outcome=\"added\"[^}]*\\} 10"));
            Assert.IsTrue(Regex.IsMatch(scrape, "merge_tile_outcomes_total\\{[^}]*outcome=\"merged\"[^}]*\\} 5"));
            Assert.IsTrue(Regex.IsMatch(scrape, "merge_tile_outcomes_total\\{[^}]*outcome=\"replaced\"[^}]*\\} 2"));
            Assert.IsTrue(Regex.IsMatch(scrape, "merge_tile_outcomes_total\\{[^}]*outcome=\"skipped\"[^}]*\\} 1"));
            Assert.IsTrue(scrape.Contains("target_format=\"PNG\""));
            Assert.IsTrue(scrape.Contains("is_new_target=\"false\""));
        }

        [TestMethod]
        public async Task TaskOutcome_ExposesCounterPerResult()
        {
            _metricsProvider.TaskOutcome("success", "MERGE");
            _metricsProvider.TaskOutcome("error", "MERGE");

            string scrape = await ScrapeAsync();

            Assert.IsTrue(scrape.Contains("merger_task_outcomes_total"), "task outcome counter missing");
            Assert.IsTrue(Regex.IsMatch(scrape, "merger_task_outcomes_total\\{[^}]*result=\"success\"[^}]*\\} 1"));
            Assert.IsTrue(Regex.IsMatch(scrape, "merger_task_outcomes_total\\{[^}]*result=\"error\"[^}]*\\} 1"));
        }

        [TestMethod]
        public async Task ReportWriteFailure_ExposesCounter()
        {
            _metricsProvider.ReportWriteFailure();

            string scrape = await ScrapeAsync();

            Assert.IsTrue(Regex.IsMatch(scrape, "merger_report_write_failures_total(\\{[^}]*\\})? 1"),
                "report write failure counter missing");
        }
    }
}
