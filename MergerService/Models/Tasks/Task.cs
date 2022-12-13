using MergerLogic.Batching;
using MergerLogic.ImageProcessing;
using MergerService.Controllers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace MergerService.Models.Tasks
{
    public enum Status
    {
        [EnumMember(Value = "Pending")] PENDING,
        [EnumMember(Value = "In-Progress")] IN_PROGRESS,
        [EnumMember(Value = "Completed")] COMPLETED,
        [EnumMember(Value = "Failed")] FAILED,
        [EnumMember(Value = "Expired")] EXPIRED,
        [EnumMember(Value = "Aborted")] ABORTED
    }

    public class MergeMetadata
    {
        [JsonInclude] public TileFormat TargetFormat { get; }
        [JsonInclude] public TileBounds[]? Batches { get; }

        [JsonInclude] public Source[]? Sources { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public MergeMetadata(TileFormat targetFormat, TileBounds[] batches, Source[] sources)
        {
            this.TargetFormat = targetFormat;
            this.Batches = batches;
            this.Sources = sources;

            this._jsonSerializerSettings = new JsonSerializerSettings();
            this._jsonSerializerSettings.Converters.Add(new StringEnumConverter());
        }

        public void Print()
        {
            Console.WriteLine(this.ToString());
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, this._jsonSerializerSettings)!;
        }
    }

    public class MergeTask
    {
        [JsonInclude] public string Id { get; }

        [JsonInclude] public string Type { get; }

        [JsonInclude] public string Description { get; }

        [JsonInclude] public MergeMetadata Parameters { get; }

        [JsonInclude] public Status Status { get; }

        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [JsonInclude]
        public int Percentage { get; set; }

        [JsonInclude] public string Reason { get; }

        [JsonInclude] public int Attempts { get; }

        [JsonInclude] public string JobId { get; }

        [JsonInclude] public bool Resettable { get; }

        [JsonInclude] public DateTime Created { get; }

        [JsonInclude] public DateTime Updated { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public MergeTask(string id, string type, string description, MergeMetadata parameters,
            Status status, int? percentage, string reason, int attempts,
            string jobId, bool resettable, DateTime created, DateTime updated)
        {
            this.Id = id;
            this.Type = type;
            this.Description = description;
            this.Parameters = parameters;
            this.Status = status;

            if (percentage is null)
            {
                percentage = 0;
            }

            this.Percentage = (int)percentage;

            this.Reason = reason;
            this.Attempts = attempts;
            this.JobId = jobId;
            this.Resettable = resettable;
            this.Created = created;
            this.Updated = updated;

            this._jsonSerializerSettings = new JsonSerializerSettings();
            this._jsonSerializerSettings.Converters.Add(new StringEnumConverter());
        }

        public void Print()
        {
            Console.WriteLine($"{this.ToString()}");
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, this._jsonSerializerSettings)!;
        }
    }
}
