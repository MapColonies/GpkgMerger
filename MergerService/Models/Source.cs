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

        [DefaultValue(GridOrigin.UPPER_LEFT)]
        [JsonProperty(Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate, NullValueHandling = NullValueHandling.Ignore)]
        public GridOrigin Origin { get; }

        [DefaultValue("2x1")]
        [JsonProperty(Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate, NullValueHandling = NullValueHandling.Ignore)]
        public string Grid { get; }

        public Source(string path, string type, GridOrigin origin = GridOrigin.UPPER_LEFT, string grid = "2x1")
        {
            this.Path = path;
            this.Type = type;
            this.Origin = origin;
            this.Grid = grid.ToLower();
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
            var jsonSerializerSettings = new JsonSerializerSettings();
            jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            return JsonConvert.SerializeObject(this, jsonSerializerSettings)!;
        }
    }
}
