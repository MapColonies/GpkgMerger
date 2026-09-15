using Amazon.S3;
using Amazon.S3.Model;
using MergerLogic.Utils;
using MergerService.Models.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using System.Reflection;

namespace MergerService.Utils
{
    public class ReportWriter : IReportWriter
    {
        private readonly IConfigurationManager _configuration;
        private readonly IFileSystem _fileSystem;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReportWriter> _logger;

        public ReportWriter(IConfigurationManager configuration, IFileSystem fileSystem, IServiceProvider serviceProvider,
            ILogger<ReportWriter> logger)
        {
            this._configuration = configuration;
            this._fileSystem = fileSystem;
            this._serviceProvider = serviceProvider;
            this._logger = logger;
        }

        public void WriteReport(MergeReport report, string? outputPath)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            if (string.IsNullOrEmpty(outputPath))
            {
                this._logger.LogDebug($"[{methodName}] No ReportOutputPath configured, skipping report artifact");
                return;
            }

            string fileName = $"merge-report-{report.JobId}-{report.TaskId}.json";
            string json = report.ToJson();
            string sink = this._configuration.GetConfiguration("REPORT", "sink");

            if (string.Equals(sink, "S3", System.StringComparison.OrdinalIgnoreCase))
            {
                this.WriteToS3(outputPath, fileName, json);
            }
            else
            {
                this.WriteToFs(outputPath, fileName, json);
            }

            this._logger.LogInformation($"[{methodName}] Wrote merge report to {sink}:{outputPath}/{fileName}");
        }

        private void WriteToFs(string outputPath, string fileName, string json)
        {
            this._fileSystem.Directory.CreateDirectory(outputPath);
            string fullPath = this._fileSystem.Path.Combine(outputPath, fileName);
            this._fileSystem.File.WriteAllText(fullPath, json);
        }

        private void WriteToS3(string outputPath, string fileName, string json)
        {
            string bucket = this._configuration.GetConfiguration("S3", "bucket");
            string key = $"{outputPath.TrimEnd('/')}/{fileName}";
            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                ContentBody = json,
                ContentType = "application/json"
            };
            var s3 = this._serviceProvider.GetRequiredService<IAmazonS3>();
            s3.PutObjectAsync(request).Wait();
        }
    }
}
