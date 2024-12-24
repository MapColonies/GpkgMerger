using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;

namespace MergerService.Models.Jobs
{
     public class AdditionalParams
    {
        [JsonInclude] public string? JobTrackerServiceURL { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public AdditionalParams(string jobTrackerServiceURL)
        {
            this.JobTrackerServiceURL = jobTrackerServiceURL;

            this._jsonSerializerSettings = new JsonSerializerSettings();
            this._jsonSerializerSettings.Converters.Add(new StringEnumConverter());
        }
    }

}
