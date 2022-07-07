using System.Diagnostics;

using MergerLogic.Utils;
using MergerService.Controllers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Text.Json;

namespace MergerService.Utils
{
    // TODO: rename all utils to clients
    public class TaskUtils : ITaskUtils
    {
        private IHttpRequestUtils _httpClient;
        private IConfigurationManager _configuration;
        private ILogger _logger;
        private ActivitySource _activitySource;

        public TaskUtils(IConfigurationManager configuration, IHttpRequestUtils httpClient, ILogger<TaskUtils> logger, ActivitySource activitySource)
        {
            this._httpClient = httpClient;
            this._configuration = configuration;
            this._logger = logger;
            this._activitySource = activitySource;
            //TODO: add tracing
        }

        // TODO: add update progress method
        public MergeTask? GetTask()
        {
            string url = this._configuration.GetConfiguration("TASK", "jobManagerUrl");
            Console.WriteLine($"url: {url}");
            string metadata = this._httpClient.GetDataString(url);
            Console.WriteLine($"metadata: {metadata}");
            // metadata.Print();
            // return metadata;

            // Area1 full batches
            // Select statement for each zoom level: select MIN(tile_column), MAX(tile_column), MIN(tile_row), MAX(tile_row) from <table> where zoom_level=<zoom>;
            // string taskJson = @"{
            //     ""Batches"": [
            //         {
            //             ""minX"": 9,
            //             ""maxX"": 10,
            //             ""minY"": 2,
            //             ""maxY"": 3,
            //             ""zoom"": 3
            //         },
            //         {
            //             ""minX"": 19,
            //             ""maxX"": 20,
            //             ""minY"": 5,
            //             ""maxY"": 6,
            //             ""zoom"": 4
            //         },
            //         {
            //             ""minX"": 38,
            //             ""maxX"": 39,
            //             ""minY"": 10,
            //             ""maxY"": 11,
            //             ""zoom"": 5
            //         },
            //         {
            //             ""minX"": 76,
            //             ""maxX"": 77,
            //             ""minY"": 20,
            //             ""maxY"": 21,
            //             ""zoom"": 6
            //         },
            //         {
            //             ""minX"": 152,
            //             ""maxX"": 153,
            //             ""minY"": 41,
            //             ""maxY"": 42,
            //             ""zoom"": 7
            //         },
            //         {
            //             ""minX"": 304,
            //             ""maxX"": 305,
            //             ""minY"": 83,
            //             ""maxY"": 84,
            //             ""zoom"": 8
            //         },
            //         {
            //             ""minX"": 609,
            //             ""maxX"": 610,
            //             ""minY"": 167,
            //             ""maxY"": 168,
            //             ""zoom"": 9
            //         },
            //         {
            //             ""minX"": 1218,
            //             ""maxX"": 1220,
            //             ""minY"": 334,
            //             ""maxY"": 335,
            //             ""zoom"": 10
            //         },
            //         {
            //             ""minX"": 2437,
            //             ""maxX"": 2439,
            //             ""minY"": 668,
            //             ""maxY"": 670,
            //             ""zoom"": 11
            //         },
            //         {
            //             ""minX"": 4875,
            //             ""maxX"": 4878,
            //             ""minY"": 1337,
            //             ""maxY"": 1339,
            //             ""zoom"": 12
            //         },
            //         {
            //             ""minX"": 9751,
            //             ""maxX"": 9755,
            //             ""minY"": 2674,
            //             ""maxY"": 2678,
            //             ""zoom"": 13
            //         },
            //         {
            //             ""minX"": 19503,
            //             ""maxX"": 19509,
            //             ""minY"": 5349,
            //             ""maxY"": 5355,
            //             ""zoom"": 14
            //         },
            //         {
            //             ""minX"": 39006,
            //             ""maxX"": 39017,
            //             ""minY"": 10698,
            //             ""maxY"": 10709,
            //             ""zoom"": 15
            //         },
            //         {
            //             ""minX"": 78012,
            //             ""maxX"": 78034,
            //             ""minY"": 21396,
            //             ""maxY"": 21417,
            //             ""zoom"": 16
            //         },
            //         {
            //             ""minX"": 156024,
            //             ""maxX"": 156068,
            //             ""minY"": 42793,
            //             ""maxY"": 42833,
            //             ""zoom"": 17
            //         },
            //         {
            //             ""minX"": 312048,
            //             ""maxX"": 312135,
            //             ""minY"": 85587,
            //             ""maxY"": 85665,
            //             ""zoom"": 18
            //         }
            //     ],
            //     ""Sources"": [
            //         {
            //             ""Path"": ""output/temp"",
            //             ""Type"": ""FS"",
            //             ""Origin"": ""UL"",
            //             ""Grid"": ""2X1""
            //         }
            //     ]
            // }";

            try
            {
                var jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add(new StringEnumConverter());
                return JsonConvert.DeserializeObject<MergeTask>(metadata, jsonSerializerSettings)!;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
                return null;
            }
        }
    }
}
