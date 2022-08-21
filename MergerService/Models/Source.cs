using MergerLogic.Batching;
using MergerLogic.DataTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MergerService.Controllers
{
    public class Source
    {
        public string Path { get; }

        public string Type { get; }

        public Extent? Extent { get; }

        public GridOrigin? Origin { get; }

        public Grid? Grid { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public Source(string path, string type, Extent? extent = null, GridOrigin? origin = null, Grid? grid = null)
        {
            this.Path = path;
            this.Type = type;
            this.Extent = extent;
            this.Origin = origin;
            this.Grid = grid;

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
