using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;

namespace MergerService.Models.Jobs
{
    public class JobMergeMetadata
    {
        [JsonInclude] public JobMetadata Metadata { get; }
        [JsonInclude] public string[] FileNames { get; }
        [JsonInclude] public string OriginDirectory { get; }
        [JsonInclude] public string LayerRelativePath { get; }
        [JsonInclude] public AdditionalParams? AdditionalParams { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public JobMergeMetadata(JobMetadata metadata, string[] fileNames, string originDirectory, string layerRelativePath,
            AdditionalParams additionalParams)
        {
            this.Metadata = metadata;
            this.FileNames = fileNames;
            this.OriginDirectory = originDirectory;
            this.LayerRelativePath = layerRelativePath;
            this.AdditionalParams = additionalParams;

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

}
