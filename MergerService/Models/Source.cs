using MergerLogic.Batching;
using MergerLogic.DataTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

namespace MergerService.Controllers
{
    public class Source
    {
        public string Path { get; }

        public string Type { get; }

        public Extent? Extent { get; }

        public GridOrigin? Origin { get; }

        [DefaultValue("2x1")]
        [JsonProperty(Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate, NullValueHandling = NullValueHandling.Ignore)]
        public string Grid { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public Source(string path, string type, Extent? extent = null, GridOrigin? origin = null, string grid = "2x1")
        {
            this.Path = path;
            this.Type = type;
            this.Extent = extent;
            this.Origin = origin;
            this.Grid = grid.ToLower();

            this._jsonSerializerSettings = new JsonSerializerSettings();
            this._jsonSerializerSettings.Converters.Add(new StringEnumConverter());
        }

        public bool IsOneXOne()
        {
            return this.Grid.ToLower() == "1x1";
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
