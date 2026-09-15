using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;

namespace MergerService.Models.Jobs
{
     public class AdditionalParams
    {
        [JsonInclude] public string? JobTrackerServiceURL { get; }
        [JsonInclude] public string? ReportOutputPath { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public AdditionalParams(string jobTrackerServiceURL, string? reportOutputPath = null)
        {
            this.JobTrackerServiceURL = jobTrackerServiceURL;
            this.ReportOutputPath = reportOutputPath;

            this._jsonSerializerSettings = new JsonSerializerSettings();
            this._jsonSerializerSettings.Converters.Add(new StringEnumConverter());
        }
    }

}
