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
        private string _overseerUrl;
        private string _jobManagerUrl;

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

            _overseerUrl = this._configuration.GetConfiguration("TASK", "overseerUrl");
            _jobManagerUrl = this._configuration.GetConfiguration("TASK", "jobManagerUrl");
            //TODO: add tracing
        }

        public MergeTask? GetTask(string jobType, string taskType)
        {
            // TODO: add heartbeat start method

            using (var dequeueActivity = this._activitySource.StartActivity("dequeue task"))
            {
                string relativeUri = $"tasks/{jobType}/{taskType}/startPending";
                string url = new Uri(new Uri(_jobManagerUrl), relativeUri).ToString();
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
                    this._logger.LogWarning(e, "Error deserializing returned task");
                    return null;
                }
            }

            // TODO: add heartbeat stop method
        }

        public void NotifyOnCompletion(string jobId, string taskId)
        {
            using (var activity = this._activitySource.StartActivity("notify overseer on task completion"))
            {
                // Notify overseer on task completion
                string relativeUri = $"tasks/{jobId}/{taskId}/completed";
                string url = new Uri(new Uri(_overseerUrl), relativeUri).ToString();
                _ = this._httpClient.PostDataString(url, null, false);
            }
        }

        private void Update(string jobId, string taskId, HttpContent content)
        {
            string relativeUri = $"jobs/{jobId}/tasks/{taskId}";
            string url = new Uri(new Uri(_jobManagerUrl), relativeUri).ToString();
            _ = this._httpClient.PutDataString(url, content, false);
        }

        public void UpdateProgress(string jobId, string taskId, int progress)
        {
            using (var activity = this._activitySource.StartActivity("update task progress"))
            {
                // activity.AddTag("progress", progress);

                var content = new StringContent(JsonConvert.SerializeObject(new
                {
                    percentage = progress
                }, this._jsonSerializerSettings));

                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Update(jobId, taskId, content);
            }
        }

        public void UpdateCompletion(string jobId, string taskId)
        {
            using (var activity = this._activitySource.StartActivity("update task completed"))
            {
                var content = new StringContent(JsonConvert.SerializeObject(new
                {
                    percentage = 100,
                    status = Status.COMPLETED
                }, this._jsonSerializerSettings));

                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Update(jobId, taskId, content);
            }
        }

        public void UpdateReject(string jobId, string taskId, int attempts, string reason, bool resettable)
        {
            using (var activity = this._activitySource.StartActivity("reject task"))
            {
                attempts++;
                // activity.AddTag("attempts", attempts);
                // activity.AddTag("resettable", resettable);

                // Check if the task should actually fail
                if (!resettable || attempts == this._maxAttempts)
                {
                    UpdateFailed(jobId, taskId, reason);
                    return;
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
        }

        private void UpdateFailed(string jobId, string taskId, string reason)
        {
            using (var activity = this._activitySource.StartActivity("fail task"))
            {
                // activity.AddTag("reason", reason);

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
}
