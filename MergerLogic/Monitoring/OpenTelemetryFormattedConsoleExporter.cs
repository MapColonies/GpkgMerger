using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using System.Text.Json;

namespace MergerLogic.Monitoring
{
    public class OpenTelemetryFormattedConsoleExporter : ConsoleExporter<LogRecord>
    {
        private const string SERVICE_NAME_ATTRIBUTE = "service.name";
        private const string SERVICE_VERSION_ATTRIBUTE = "service.version";


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

            var entry = new Dictionary<string, object?>
            {
                ["time"] = this.FormatTime(record.Timestamp),
                ["level"] = record.LogLevel.ToString(),
                ["service"] = serviceName,
                ["version"] = serviceVersion,
                ["category"] = record.CategoryName,
                ["thread"] = Environment.CurrentManagedThreadId,
                ["message"] = record.State?.ToString(),
            };

            if (record.Exception != null)
            {
                entry["exception"] = record.Exception.ToString();
            }

            this.AddScopes(record, entry);

            return JsonSerializer.Serialize(entry);
        }

        // Flatten ILogger.BeginScope key/value pairs to top-level fields (e.g. jobId/taskId) so they
        // are queryable in Loki. Empty unless IncludeScopes is enabled (see DI setup).
        private void AddScopes(LogRecord record, Dictionary<string, object?> entry)
        {
            record.ForEachScope((scope, state) =>
            {
                foreach (var pair in scope)
                {
                    if (pair.Key == "{OriginalFormat}")
                    {
                        continue;
                    }

                    state[pair.Key] = pair.Value;
                }
            }, entry);
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

        private string? GetResourceAttribute(Dictionary<string, object> resource, string attribute, string defaultValue)
        {
            return resource.ContainsKey(attribute) ? resource[attribute]?.ToString() : defaultValue;
        }

    }
}
