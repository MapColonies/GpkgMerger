using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
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

        // Classifies a single merged tile into added / merged / replaced / skipped.
        // added = tile didn't exist in target before the merge; merged = existed and the
        // target was blended with source data; replaced = existed and an opaque source
        // covered it; skipped = no tile produced or no source data contributed.
        public void RecordOutcome(Coord coord, bool existedBefore, bool tileProduced, MergeStats stats)
        {
            if (!tileProduced || !stats.AnySourceUsed)
            {
                this.RecordSkipped();
                return;
            }

            if (!existedBefore)
            {
                this.RecordAdded(coord);
                return;
            }

            if (stats.TargetUsed)
            {
                this.RecordMerged();
                return;
            }

            this.RecordReplaced();
        }

        private void RecordAdded(Coord coord)
        {
            this.Added++;
            // store a copy so a later in-place coord mutation can't corrupt the list
            this.AddedTiles.Add(new Coord(coord.Z, coord.X, coord.Y));
        }

        private void RecordMerged() => this.Merged++;
        private void RecordReplaced() => this.Replaced++;
        private void RecordSkipped() => this.Skipped++;

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
