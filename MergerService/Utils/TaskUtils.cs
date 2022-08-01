using System.Diagnostics;
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
            string taskData = this._httpClient.PostDataString(url, null);

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

        public void NotifyOnCompletion(string jobId, string taskId)
        {
            // Notify overseer on task completion
            string baseUrl = this._configuration.GetConfiguration("TASK", "overseerUrl");
            string url = Path.Combine(baseUrl, $"tasks/{jobId}/{taskId}/completed");
            _ = this._httpClient.PostDataString(url, null);
        }

        private void Update(string jobId, string taskId, FormUrlEncodedContent content)
        {
            string baseUrl = this._configuration.GetConfiguration("TASK", "jobManagerUrl");
            string url = Path.Combine(baseUrl, $"jobs/{jobId}/tasks/{taskId}");
            _ = this._httpClient.PutDataString(url, content);
        }

        public void UpdateProgress(string jobId, string taskId, int progress)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("percentage", progress.ToString())
            });
            Update(jobId, taskId, content);
        }

        public void UpdateCompletion(string jobId, string taskId)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("percentage", "100"),
                new KeyValuePair<string, string>("status", "completed")
            });
            Update(jobId, taskId, content);
        }

        public void UpdateReject(string jobId, string taskId, int attempts, string reason, bool resettable)
        {
            attempts++;
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("attempts", attempts.ToString()),
                new KeyValuePair<string, string>("reason", reason),
                new KeyValuePair<string, string>("resettable", resettable.ToString())
            });
            Update(jobId, taskId, content);
        }

        public void UpdateFailed(string jobId, string taskId, string reason)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("status", "failed"),
                new KeyValuePair<string, string>("reason", reason)
            });
            Update(jobId, taskId, content);
        }
    }
}
