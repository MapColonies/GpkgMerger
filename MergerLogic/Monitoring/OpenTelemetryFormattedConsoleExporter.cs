using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using System;
using System.Net;

namespace MergerLogic.Monitoring
{
    public class OpenTelemetryFormattedConsoleExporter : ConsoleExporter<LogRecord>
    {
        private const string SERVICE_NAME_ATTRIBUTE = "service.name";
        private const string SERVICE_VERSION_ATTRIBUTE = "service.version";
        private const string SERVICE_HOST_NAME_ATTRIBUTE = "service.host.name";


        public OpenTelemetryFormattedConsoleExporter(ConsoleExporterOptions options) : base(options)
        {
        }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var logRecord in batch)
            {
                string log = this.MCTextFormat(logRecord);
                this.WriteLine(log);
            }
            return ExportResult.Success;
        }

        private string MCTextFormat(LogRecord record)
        {
            var resource = this.ParseResource();
            var serviceName = this.GetResourceAttribute(resource, SERVICE_NAME_ATTRIBUTE, "unknown_service");
            var serviceVersion = this.GetResourceAttribute(resource, SERVICE_VERSION_ATTRIBUTE, "unknown_version");
            if (!resource.ContainsKey(SERVICE_HOST_NAME_ATTRIBUTE))
            {
                resource.Add(SERVICE_HOST_NAME_ATTRIBUTE, Dns.GetHostName());
            }
            var serviceHostName = this.GetResourceAttribute(resource, SERVICE_HOST_NAME_ATTRIBUTE, "unknown_host_name");
            var exception = record.Exception != null ? $" [{record.Exception}]" : string.Empty;
            return $"[{this.FormatTime(record.Timestamp)}] [{record.LogLevel}] [{serviceName}] [{serviceHostName}] [{serviceVersion}] [{Environment.CurrentManagedThreadId}] [{record.CategoryName}] {record.State}{exception}";
        }

        private string FormatTime(DateTime time)
        {
            return time.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }

        private Dictionary<string, object> ParseResource()
        {
            var attributes = this.ParentProvider.GetResource()?.Attributes;
            return attributes != null ? new Dictionary<string, object>(attributes) : new Dictionary<string, object>();
        }

        private string GetResourceAttribute(Dictionary<string, object> resource, string attribute, string defaultValue)
        {
            return resource.ContainsKey(attribute) ? resource[attribute]?.ToString() : defaultValue;
        }

    }
}
