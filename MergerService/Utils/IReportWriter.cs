using MergerService.Models.Reports;

namespace MergerService.Utils
{
    public interface IReportWriter
    {
        // Writes the report JSON artifact to the configured sink under outputPath.
        // No-op when outputPath is null/empty. Throws on write failure.
        void WriteReport(MergeReport report, string? outputPath);
    }
}
