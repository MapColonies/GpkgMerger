using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Utils;
using MergerService.Models.Reports;
using MergerService.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MergerServiceUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    public class ReportWriterTest
    {
        private Mock<IConfigurationManager> _config;
        private Mock<ILogger<ReportWriter>> _logger;
        private Mock<IAmazonS3> _s3;
        private Mock<IServiceProvider> _serviceProvider;

        [TestInitialize]
        public void BeforeEach()
        {
            this._config = new Mock<IConfigurationManager>(MockBehavior.Loose);
            this._logger = new Mock<ILogger<ReportWriter>>(MockBehavior.Loose);
            this._s3 = new Mock<IAmazonS3>(MockBehavior.Loose);
            this._serviceProvider = new Mock<IServiceProvider>(MockBehavior.Loose);
            this._serviceProvider.Setup(sp => sp.GetService(typeof(IAmazonS3))).Returns(this._s3.Object);
        }

        private MergeReport BuildReport()
        {
            var r = new MergeReport("job1", "task1", "MERGE", "PNG", false);
            r.Finalize(System.DateTime.UnixEpoch, System.DateTime.UnixEpoch);
            return r;
        }

        [TestMethod]
        public void FsSink_WritesJsonFile()
        {
            this._config.Setup(c => c.GetConfiguration("REPORT", "sink")).Returns("FS");
            var fs = new MockFileSystem();
            var writer = new ReportWriter(this._config.Object, fs, this._serviceProvider.Object, this._logger.Object);

            writer.WriteReport(this.BuildReport(), "/reports");

            string expected = fs.Path.Combine("/reports", "merge-report-job1-task1.json");
            Assert.IsTrue(fs.FileExists(expected));
            StringAssert.Contains(fs.File.ReadAllText(expected), "\"jobId\":\"job1\"");
        }

        [TestMethod]
        public void S3Sink_PutsObject()
        {
            this._config.Setup(c => c.GetConfiguration("REPORT", "sink")).Returns("S3");
            this._config.Setup(c => c.GetConfiguration("S3", "bucket")).Returns("tiles");
            this._s3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PutObjectResponse());
            var fs = new MockFileSystem();
            var writer = new ReportWriter(this._config.Object, fs, this._serviceProvider.Object, this._logger.Object);

            writer.WriteReport(this.BuildReport(), "reports");

            this._s3.Verify(s => s.PutObjectAsync(
                It.Is<PutObjectRequest>(r => r.BucketName == "tiles" && r.Key == "reports/merge-report-job1-task1.json"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void EmptyPath_IsNoOp()
        {
            var fs = new MockFileSystem();
            var writer = new ReportWriter(this._config.Object, fs, this._serviceProvider.Object, this._logger.Object);

            writer.WriteReport(this.BuildReport(), null);
            writer.WriteReport(this.BuildReport(), "");

            Assert.AreEqual(0, fs.AllFiles.Count());
            this._s3.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
