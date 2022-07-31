using System.Diagnostics;
using System.Text;
using MergerLogic.Utils;
using MergerService.Controllers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
        public MergeTask? GetTask(string jobType, string taskType)
        {
            string baseUrl = this._configuration.GetConfiguration("TASK", "jobManagerUrl");
            string url = $"{baseUrl}/tasks/{jobType}/{taskType}/startPending";
            string taskData = this._httpClient.PostDataString(url);

            if (taskData is null)
            {
                return null;
            }

            try
            {
                var jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.Converters.Add(new StringEnumConverter());
                return JsonConvert.DeserializeObject<MergeTask>(taskData, jsonSerializerSettings)!;
            }
            catch (Exception e)
            {
                this._logger.LogInformation(e, "Error serializing returned task");
                return null;
            }
        }

        public void UpdateTask(string jobId, string taskId, UpdateParameters updateParameters)
        {
            // Update job DB on task completion
            string baseUrl = this._configuration.GetConfiguration("TASK", "jobManagerUrl");
            string url = Path.Combine(baseUrl, $"jobs/{jobId}/tasks/{taskId}");

            // Convert metadata to json
            var jsonSerializerSettings = new JsonSerializerSettings();
            jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            string json = JsonConvert.SerializeObject(updateParameters, jsonSerializerSettings);
            var body = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");

            _ = this._httpClient.PutDataString(url, body);
        }

        public void UpdateCompletion(string jobId, string taskId)
        {
            // Update overseer on task completion
            string baseUrl = this._configuration.GetConfiguration("TASK", "overseerUrl");
            string url = Path.Combine(baseUrl, $"tasks/{jobId}/{taskId}/completed");
            _ = this._httpClient.PostDataString(url);
        }
    }
}
