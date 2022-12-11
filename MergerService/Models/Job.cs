using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MergerService.Controllers
{
    public class Footprint
    {
        [JsonInclude] public string Type { get; }
        [JsonInclude] public double[][][] Coordinates { get; }
        private JsonSerializerSettings _jsonSerializerSettings;

        public Footprint(string type, double[][][] coordinates)
        {
            this.Type = type;
            this.Coordinates = coordinates;

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
    public class FeatureProperties
    {
        [JsonInclude] public string Dsc { get; }
        [JsonInclude] public double Rms { get; }
        [JsonInclude] public double Ep90 { get; }
        [JsonInclude] public int Scale { get; }
        [JsonInclude] public string Cities { get; }
        [JsonInclude] public string Source { get; }
        [JsonInclude] public string Countries { get; }
        [JsonInclude] public string Resolution { get; }
        [JsonInclude] public string SensorType { get; }
        [JsonInclude] public string SourceName { get; }
        private JsonSerializerSettings _jsonSerializerSettings;

        public FeatureProperties(string dsc, double rms, double ep90, int scale, string cities, string source,
                                string countries, string resolution, string sensorType, string sourceName)
        {
            this.Dsc = dsc;
            this.Rms = rms;
            this.Ep90 = ep90;
            this.Scale = scale;
            this.Cities = cities;
            this.Source = source;
            this.Countries = countries;
            this.Resolution = resolution;
            this.SensorType = sensorType;
            this.SourceName = sourceName;


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
    public class Feature
    {
        [JsonInclude] public string Type { get; }
        [JsonInclude] public Footprint Geometry { get; }
        [JsonInclude] public FeatureProperties FeaturesProperties { get; }

        private JsonSerializerSettings _jsonSerializerSettings;

        public Feature(string type, Footprint geometry, FeatureProperties featuresProperties)
        {
            this.Type = type;
            this.Geometry = geometry;
            this.FeaturesProperties = featuresProperties;

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
    public class LayerPolygonParts
    {
        [JsonInclude] public double[] Bbox { get; }
        [JsonInclude] public string Type { get; }
        [JsonInclude] public Feature[] Features { get; }
        private JsonSerializerSettings _jsonSerializerSettings;

        public LayerPolygonParts(double[] bbox, string type, Feature[] features)
        {
            this.Bbox = bbox;
            this.Type = type;
            this.Features = features;

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
        [JsonInclude] public Footprint Footprint { get; }
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
        [JsonInclude] public LayerPolygonParts LayerPolygonParts { get; }
        [JsonInclude] public double MaxResolutionMeter { get; }
        [JsonInclude] public string ProductBoundingBox { get; }
        [JsonInclude] public double MinHorizontalAccuracyCE90 { get; }


        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public JobMetadata(string id, double rms, string type, int scale, string srsId, string[] region,
            string[] sensors, string srsName, Footprint footprint, string productId, string description,
            string displayPath, string productName, string productType, DateTime creationDate, string producerName,
            DateTime ingestionDate, DateTime sourceDateEnd, string classification, string productSubType,
            string productVersion, DateTime sourceDateStart, double maxResolutionDeg, LayerPolygonParts layerPolygonParts,
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
    public class JobMergeMetadata
    {
        [JsonInclude] public JobMetadata Metadata { get; }
        [JsonInclude] public string[] FileNames { get; }
        [JsonInclude] public string OriginDirectory { get; }
        [JsonInclude] public string LayerRelativePath { get; }
        [JsonInclude] public string? ManagerCallbackUrl { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        private JsonSerializerSettings _jsonSerializerSettings;

        public JobMergeMetadata(JobMetadata metadata, string[] fileNames,string originDirectory, string layerRelativePath,
            string managerCallbackUrl)
        {
            this.Metadata = metadata;
            this.FileNames = fileNames;
            this.OriginDirectory = originDirectory;
            this.LayerRelativePath = layerRelativePath;
            this.ManagerCallbackUrl = managerCallbackUrl;


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