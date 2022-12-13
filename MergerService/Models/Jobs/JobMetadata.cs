using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;
using BAMCIS.GeoJSON;

namespace MergerService.Models.Jobs
{
     public class JobMetadata
    {
        [JsonInclude] public string Id { get; }
        [JsonInclude] public double Rms { get; }
        [JsonInclude] public string Type { get; }
        [JsonInclude] public int Scale { get; }
        [JsonInclude] public string SrsId { get; }
        [JsonInclude] public string[] Region { get; }
        [JsonInclude] public string[] Sensors { get; }
        [JsonInclude] public string SrsName { get; }
        [JsonInclude] public GeoJson Footprint { get; }
        [JsonInclude] public string ProductId { get; }
        [JsonInclude] public string Description { get; }
        [JsonInclude] public string DisplayPath { get; }
        [JsonInclude] public string ProductName { get; }
        [JsonInclude] public string ProductType { get; }
        [JsonInclude] public DateTime? CreationDate { get; }
        [JsonInclude] public string ProducerName { get; }
        [JsonInclude] public DateTime? IngestionDate { get; }
        [JsonInclude] public DateTime? SourceDateEnd { get; }
        [JsonInclude] public string Classification { get; }
        [JsonInclude] public string ProductSubType { get; }
        [JsonInclude] public string ProductVersion { get; }
        [JsonInclude] public DateTime? SourceDateStart { get; }
        [JsonInclude] public double MaxResolutionDeg { get; }
        [JsonInclude] public FeatureCollection LayerPolygonParts { get; }
        [JsonInclude] public double MaxResolutionMeter { get; }
        [JsonInclude] public string ProductBoundingBox { get; }
        [JsonInclude] public double MinHorizontalAccuracyCE90 { get; }


        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public JobMetadata(string id, double rms, string type, int scale, string srsId, string[] region,
            string[] sensors, string srsName, GeoJson footprint, string productId, string description,
            string displayPath, string productName, string productType, DateTime creationDate, string producerName,
            DateTime ingestionDate, DateTime sourceDateEnd, string classification, string productSubType,
            string productVersion, DateTime sourceDateStart, double maxResolutionDeg, FeatureCollection layerPolygonParts,
            double maxResolutionMeter, string productBoundingBox, double minHorizontalAccuracyCE90)
        {
            this.Id = id;
            this.Rms = rms;
            this.Type = type;
            this.Scale = scale;
            this.SrsId = srsId;
            this.Region = region;
            this.Sensors = sensors;
            this.SrsName = srsName;
            this.Footprint = footprint;
            this.ProductId = productId;
            this.Description = description;
            this.DisplayPath = displayPath;
            this.ProductName = productName;
            this.ProductType = productType;
            this.CreationDate = creationDate;
            this.ProducerName = producerName;
            this.IngestionDate = ingestionDate;
            this.SourceDateEnd = sourceDateEnd;
            this.Classification = classification;
            this.ProductSubType = productSubType;
            this.ProductVersion = productVersion;
            this.SourceDateStart = sourceDateStart;
            this.MaxResolutionDeg = maxResolutionDeg;
            this.LayerPolygonParts = layerPolygonParts;
            this.MaxResolutionMeter = maxResolutionMeter;
            this.ProductBoundingBox = productBoundingBox;
            this.MinHorizontalAccuracyCE90 = minHorizontalAccuracyCE90;

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
