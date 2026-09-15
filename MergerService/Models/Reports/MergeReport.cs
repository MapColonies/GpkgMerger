using MergerLogic.DataTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MergerService.Models.Reports
{
    public class MergeReport
    {
        public int Version => 1;
        public string JobId { get; }
        public string TaskId { get; }
        public string TaskType { get; }
        public string TargetFormat { get; }
        public bool IsNewTarget { get; }

        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public double DurationSeconds { get; private set; }

        public int Added { get; private set; }
        public int Merged { get; private set; }
        public int Replaced { get; private set; }
        public int Skipped { get; private set; }
        public int Total => this.Added + this.Merged + this.Replaced + this.Skipped;

        public double AddedPercentage { get; private set; }
        public double MergedPercentage { get; private set; }
        public double ReplacedPercentage { get; private set; }
        public double SkippedPercentage { get; private set; }

        [JsonProperty("addedTiles")]
        public List<Coord> AddedTiles { get; } = new List<Coord>();

        public MergeReport(string jobId, string taskId, string taskType, string targetFormat, bool isNewTarget)
        {
            this.JobId = jobId;
            this.TaskId = taskId;
            this.TaskType = taskType;
            this.TargetFormat = targetFormat;
            this.IsNewTarget = isNewTarget;
        }

        public void RecordAdded(Coord coord)
        {
            this.Added++;
            this.AddedTiles.Add(coord);
        }

        public void RecordMerged() => this.Merged++;
        public void RecordReplaced() => this.Replaced++;
        public void RecordSkipped() => this.Skipped++;

        public void Finalize(DateTime startTime, DateTime endTime)
        {
            this.StartTime = startTime;
            this.EndTime = endTime;
            this.DurationSeconds = (endTime - startTime).TotalSeconds;

            int total = this.Total;
            if (total > 0)
            {
                this.AddedPercentage = 100.0 * this.Added / total;
                this.MergedPercentage = 100.0 * this.Merged / total;
                this.ReplacedPercentage = 100.0 * this.Replaced / total;
                this.SkippedPercentage = 100.0 * this.Skipped / total;
            }
        }

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None,
            new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

        // Summary for the structured log line: counts + percentages, WITHOUT the added-tile list.
        public string ToLogString()
        {
            var summary = new
            {
                version = this.Version,
                jobId = this.JobId,
                taskId = this.TaskId,
                taskType = this.TaskType,
                targetFormat = this.TargetFormat,
                isNewTarget = this.IsNewTarget,
                durationSeconds = this.DurationSeconds,
                counts = new { added = this.Added, merged = this.Merged, replaced = this.Replaced, skipped = this.Skipped, total = this.Total },
                percentages = new { added = this.AddedPercentage, merged = this.MergedPercentage, replaced = this.ReplacedPercentage, skipped = this.SkippedPercentage }
            };
            return JsonConvert.SerializeObject(summary, Formatting.None);
        }
    }
}
