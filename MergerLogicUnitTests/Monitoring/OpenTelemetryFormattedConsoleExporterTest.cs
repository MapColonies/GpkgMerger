using MergerLogic.Monitoring;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MergerLogicUnitTests.Monitoring
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("monitoring")]
    public class OpenTelemetryFormattedConsoleExporterTest
    {
        private TextWriter _originalOut = null!;

        [TestInitialize]
        public void BeforeEach()
        {
            this._originalOut = Console.Out;
        }

        [TestCleanup]
        public void AfterEach()
        {
            Console.SetOut(this._originalOut);
        }

        private static ILoggerFactory BuildFactory()
        {
            return LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.AddOpenTelemetry(options =>
                {
                    options.IncludeScopes = true;
                    options.AddProcessor(new SimpleLogRecordExportProcessor(
                        new OpenTelemetryFormattedConsoleExporter(new ConsoleExporterOptions())));
                });
            });
        }

        private static JsonElement CaptureSingleLine(Action<ILogger> log)
        {
            var writer = new StringWriter();
            Console.SetOut(writer);

            using (var factory = BuildFactory())
            {
                log(factory.CreateLogger("TestCategory"));
            }

            string line = writer.ToString().Trim();
            return JsonDocument.Parse(line).RootElement;
        }

        [TestMethod]
        public void WhenLoggingInsideAScope_ShouldEmitScopeAsTopLevelJsonFields()
        {
            JsonElement entry = CaptureSingleLine(logger =>
            {
                using (logger.BeginScope(new Dictionary<string, object>
                {
                    ["jobId"] = "job-123",
                    ["taskId"] = "task-456"
                }))
                {
                    logger.LogInformation("processing tiles");
                }
            });

            Assert.AreEqual("processing tiles", entry.GetProperty("message").GetString());
            Assert.AreEqual("job-123", entry.GetProperty("jobId").GetString());
            Assert.AreEqual("task-456", entry.GetProperty("taskId").GetString());
            Assert.AreEqual("Information", entry.GetProperty("level").GetString());
        }

        [TestMethod]
        public void WhenLoggingWithoutAScope_ShouldNotEmitScopeFields()
        {
            JsonElement entry = CaptureSingleLine(logger => logger.LogInformation("no scope here"));

            Assert.AreEqual("no scope here", entry.GetProperty("message").GetString());
            Assert.IsFalse(entry.TryGetProperty("jobId", out _), "unexpected jobId field");
            Assert.IsFalse(entry.TryGetProperty("taskId", out _), "unexpected taskId field");
        }
    }
}
