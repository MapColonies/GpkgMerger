using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;
using System.Text.Json.Serialization;
using MergerService.Controllers;
using MergerService.Models.Tasks;

namespace MergerService.Models.Jobs
{
    public class MergeJob
    {
        [JsonInclude] public string Id { get; }
        [JsonInclude] public string ResourceId { get; }
        [JsonInclude] public string Version { get; }
        [JsonInclude] public string Type { get; }
        [JsonInclude] public string Resolution { get; }
        [JsonInclude] public string Description { get; }
        [JsonInclude] public JobMergeMetadata Parameters { get; }
        [JsonInclude] public DateTime? CreationTime { get; }
        [JsonInclude] public DateTime? UpdateTime { get; }
        [JsonInclude] public Status Status { get; }

        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [JsonInclude]
        public int Percentage { get; set; }
        [JsonInclude] public string Reason { get; }
        [JsonInclude] public bool IsCleaned { get; }
        [JsonInclude] public int Priority { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        public DateTime? ExpirationDate { get;}
        [JsonInclude] public string InternalId { get; }
        [JsonInclude] public string ProducerName { get; }
        [JsonInclude] public string ProductName { get; }
        [JsonInclude] public string ProductType { get; }
        [JsonInclude] public int TaskCount { get; }
        [JsonInclude] public int CompletedTasks { get; }
        [JsonInclude] public int FailedTasks { get; }
        [JsonInclude] public int ExpiredTasks { get; }
        [JsonInclude] public int PendingTasks { get; }
        [JsonInclude] public int InProgressTasks { get; }
        [JsonInclude] public int AbortedTasks { get; }
        [JsonInclude] public string AdditionalIdentifiers { get; }
        [JsonInclude] public string Domain { get; }
        [JsonInclude] public MergeTask[] Tasks { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public MergeJob(string id, string resourceId, string version, string type, string resolution,
            string description, JobMergeMetadata parameters, DateTime creationTime, DateTime updateTime,
            Status status, int? percentage, string reason, bool isCleaned, int priority,
            string internalId, string producerName, string productName, string productType, int taskCount, int completedTasks, 
            int failedTasks, int expiredTasks, int pendingTasks, int inProgressTasks, int abortedTasks,
            string additionalIdentifiers, string domain, MergeTask[] tasks, DateTime? expirationDate = null)
        {
            this.Id = id;
            this.ResourceId = resourceId;
            this.Version = version;
            this.Type = type;
            this.Resolution = resolution;
            this.Description = description;
            this.Parameters = parameters;
            this.CreationTime = creationTime;
            this.UpdateTime = updateTime;
            this.Status = status;

            if (percentage is null)
            {
                percentage = 0;
            }
            this.Percentage = (int)percentage;
            this.Reason = reason;
            this.IsCleaned = isCleaned;
            this.Priority = priority;
            this.ExpirationDate = expirationDate;
            this.InternalId = internalId;
            this.ProducerName = producerName;
            this.ProductName = productName;
            this.ProductType = productType;
            this.TaskCount = taskCount;
            this.CompletedTasks = completedTasks;
            this.FailedTasks = failedTasks;
            this.ExpiredTasks = expiredTasks;
            this.PendingTasks = pendingTasks;
            this.InProgressTasks = inProgressTasks;
            this.AbortedTasks = abortedTasks;
            this.AdditionalIdentifiers = additionalIdentifiers;
            this.Domain = domain;
            this.Tasks = tasks;

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
