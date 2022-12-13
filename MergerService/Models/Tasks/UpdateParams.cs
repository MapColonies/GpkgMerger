using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MergerService.Models.Tasks
{
    public class UpdateParams
    {
        [JsonInclude] public Status Status { get; set; }

        [JsonInclude] public string? Description { get; set; }

        [JsonInclude] public MergeMetadata? Parameters { get; set; }

        [DefaultValue(0)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [JsonInclude]
        public int? Percentage { get; set; }

        [JsonInclude] public string? Reason { get; set; }

        [JsonInclude] public int? Attempts { get; set; }
        
        [JsonInclude] public bool? Resettable { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;
        
        // public UpdateParams(Status status , string? description, MergeMetadata? parameters,
        //     int? percentage, string? reason, int? attempts,
        //     bool? resettable)
        // {
        //     this.Status = status;
        //     this.Description = description;
        //     this.Parameters = parameters;
        //
        //     percentage ??= 0;
        //     this.Percentage = (int)percentage;
        //
        //     this.Reason = reason;
        //     this.Attempts = attempts;
        //     this.Resettable = resettable;
        //
        //     this._jsonSerializerSettings = new JsonSerializerSettings();
        //     this._jsonSerializerSettings.Converters.Add(new StringEnumConverter());
        // }
    }
}
