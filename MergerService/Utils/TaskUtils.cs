using System.Diagnostics;

namespace MergerService.Utils
{
    // TODO: rename all utils to clients
    public class TaskUtils
    {
        private ILogger _logger;
        private ActivitySource _activitySource;

        public TaskUtils(ILogger<TaskUtils> logger, ActivitySource activitySource)
        {
            this._logger = logger;
            this._activitySource = activitySource;
            //TODO: add tracing and logging
        }

        // TODO: add update progress method
        public static string GetTask()
        {
            return @"{
                ""Batches"": [
                    {
                        ""maxX"": 312254,
                        ""maxY"": 85399,
                        ""minX"": 312239,
                        ""minY"": 85365,
                        ""zoom"": 18
                    }
                ],
                ""Sources"": [
                    {
                        ""Path"": ""output/temp"",
                        ""Type"": ""FS"",
                        ""Origin"": ""LL"",
                        ""Grid"": ""2X1""
                    },
                    {
                        ""Path"": ""area2.gpkg"",
                        ""Type"": ""GPKG""
                    }
                ]
            }";
        }
    }
}
