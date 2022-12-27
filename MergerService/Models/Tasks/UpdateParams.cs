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
        
        public UpdateParams()
        {
            this._jsonSerializerSettings = new JsonSerializerSettings();
            this._jsonSerializerSettings.Converters.Add(new StringEnumConverter());
        }
        
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, this._jsonSerializerSettings)!;
        }
    }
}
