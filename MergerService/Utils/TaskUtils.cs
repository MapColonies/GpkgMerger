using System.Diagnostics;
using System.Net.Http.Headers;
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
        private int _maxAttempts;
        private JsonSerializerSettings _jsonSerializerSettings;

        public TaskUtils(IConfigurationManager configuration, IHttpRequestUtils httpClient, ILogger<TaskUtils> logger, ActivitySource activitySource)
        {
            this._httpClient = httpClient;
            this._configuration = configuration;
            this._logger = logger;
            this._activitySource = activitySource;
            this._maxAttempts = this._configuration.GetConfiguration<int>("TASK", "maxAttempts");

            // Construct Json serializer settings
            _jsonSerializerSettings = new JsonSerializerSettings();
            _jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            //TODO: add tracing
        }

        public MergeTask? GetTask(string jobType, string taskType)
        {
            string baseUrl = this._configuration.GetConfiguration("TASK", "jobManagerUrl");
            string relativeUri = $"tasks/{jobType}/{taskType}/startPending";
            string url = new Uri(new Uri(baseUrl), relativeUri).ToString();
            string? taskData = this._httpClient.PostDataString(url, null, false);

            if (taskData is null)
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<MergeTask>(taskData, this._jsonSerializerSettings)!;
            }
            catch (Exception e)
            {
                this._logger.LogWarning(e, "Error serializing returned task");
                return null;
            }
        }

        public void NotifyOnCompletion(string jobId, string taskId)
        {
            // Notify overseer on task completion
            string baseUrl = this._configuration.GetConfiguration("TASK", "overseerUrl");
            string relativeUri = $"tasks/{jobId}/{taskId}/completed";
            string url = new Uri(new Uri(baseUrl), relativeUri).ToString();
            _ = this._httpClient.PostDataString(url, null, false);
        }

        private void Update(string jobId, string taskId, HttpContent content)
        {
            string baseUrl = this._configuration.GetConfiguration("TASK", "jobManagerUrl");
            string relativeUri = $"jobs/{jobId}/tasks/{taskId}";
            string url = new Uri(new Uri(baseUrl), relativeUri).ToString();
            _ = this._httpClient.PutDataString(url, content, false);
        }

        public void UpdateProgress(string jobId, string taskId, int progress)
        {
            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                percentage = progress
            }, this._jsonSerializerSettings));

            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Update(jobId, taskId, content);
        }

        public void UpdateCompletion(string jobId, string taskId)
        {
            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                percentage = 100,
                status = Status.COMPLETED
            }, this._jsonSerializerSettings));

            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Update(jobId, taskId, content);
        }

        public void UpdateReject(string jobId, string taskId, int attempts, string reason, bool resettable)
        {
            attempts++;

            // Check if the task should actually fail
            if (!resettable || attempts == this._maxAttempts)
            {
                UpdateFailed(jobId, taskId, reason);
            }

            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                attempts,
                reason,
                resettable
            }, this._jsonSerializerSettings));

            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Update(jobId, taskId, content);
        }

        public void UpdateFailed(string jobId, string taskId, string reason)
        {
            var content = new StringContent(JsonConvert.SerializeObject(new
            {
                status = Status.FAILED,
                reason
            }, this._jsonSerializerSettings));

            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Update(jobId, taskId, content);
        }
    }
}
