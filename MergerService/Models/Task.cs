using MergerLogic.Batching;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace MergerService.Controllers
{
    public enum Status
    {
        [EnumMember(Value = "Pending")]
        PENDING,
        [EnumMember(Value = "In-Progress")]
        IN_PROGRESS,
        [EnumMember(Value = "Completed")]
        COMPLETED,
        [EnumMember(Value = "Failed")]
        FAILED,
        [EnumMember(Value = "Expired")]
        EXPIRED,
        [EnumMember(Value = "Aborted")]
        ABORTED
    }

    public class MergeMetadata
    {
        [JsonInclude]
        public TileBounds[]? Batches { get; }

        [JsonInclude]
        public Source[]? Sources { get; }

        public MergeMetadata(TileBounds[] batches, Source[] sources)
        {
            this.Batches = batches;
            this.Sources = sources;
        }

        public void Print()
        {
            if (this.Sources is null)
            {
                return;
            }

            Console.WriteLine("Sources:");
            foreach (Source source in this.Sources)
            {
                source.Print();
            }

            if (this.Batches is null)
            {
                return;
            }

            Console.WriteLine("Batches:");
            foreach (TileBounds bounds in this.Batches)
            {
                bounds.Print();
            }
        }

        public override string ToString()
        {
            return $@"Sources:
            {this.Sources?.ToString()}
            Batches:
            {this.Batches?.ToString()}";
        }
    }

    public class MergeTask
    {
        [JsonInclude]
        public string Id { get; }

        [JsonInclude]
        public string Type { get; }

        [JsonInclude]
        public string Description { get; }

        [JsonInclude]
        public MergeMetadata Parameters { get; }

        [JsonInclude]
        public Status Status { get; }

        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [JsonInclude]
        public int Percentage { get; set; }

        [JsonInclude]
        public string Reason { get; }

        [JsonInclude]
        public int Attempts { get; }

        [JsonInclude]
        public string JobId { get; }

        [JsonInclude]
        public bool Resettable { get; }

        [JsonInclude]
        public DateTime Created { get; }

        [JsonInclude]
        public DateTime Updated { get; }

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
        }

        public void Print()
        {
            Console.WriteLine($"{this.ToString()}");
        }

        public override string ToString()
        {
            return $@"Id: {this.Id}
            Type: {this.Type}
            Description: {this.Description}
            Parameters: {this.Parameters.ToString()}
            Status: {this.Status}
            Percentage: {this.Percentage}
            Reason: {this.Reason}
            Attempts: {this.Attempts}
            JobId: {this.JobId}
            Resettable: {this.Resettable}
            Created: {this.Created}
            Updated: {this.Updated}";
        }
    }
}
