using MergerLogic.Clients;
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
        private readonly IHttpRequestUtils _httpClient;
        private readonly IConfigurationManager _configuration;
        private  readonly ILogger _logger;
        private readonly IHeartbeatClient _heartbeatClient;
        private readonly ActivitySource _activitySource;
        private readonly int _maxAttempts;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly string _jobManagerUrl;

        public TaskUtils(IConfigurationManager configuration, IHttpRequestUtils httpClient, ILogger<TaskUtils> logger,
            ActivitySource activitySource, IHeartbeatClient heartbeatClient)
        {
            this._httpClient = httpClient;
            this._configuration = configuration;
            this._logger = logger;
            this._heartbeatClient = heartbeatClient;
            this._activitySource = activitySource;
            this._maxAttempts = this._configuration.GetConfiguration<int>("TASK", "maxAttempts");

            // Construct Json serializer settings
            _jsonSerializerSettings = new JsonSerializerSettings();
            _jsonSerializerSettings.Converters.Add(new StringEnumConverter());

            _jobManagerUrl = this._configuration.GetConfiguration("TASK", "jobManagerUrl");
            //TODO: add tracing
        }

        public MergeTask? GetTask(string jobType, string taskType)
        {
            using (this._activitySource.StartActivity("dequeue task"))
            {
                string relativeUri = $"tasks/{jobType}/{taskType}/startPending";
                string url = new Uri(new Uri(_jobManagerUrl), relativeUri).ToString();
                string? taskData = this._httpClient.PostData(url, null);

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
        }

        private void NotifyOnStatusChange(string jobId, string taskId, string managerCallbackUrl)
        {
            using (this._activitySource.StartActivity("notify overseer on task completion"))
            {
                // Notify overseer on task completion
                this._logger.LogInformation($"Notifying overseer on completion, job: {jobId}, task: {taskId}");
                string relativeUri = $"tasks/{jobId}/{taskId}/completed";
                string url = new Uri(new Uri(managerCallbackUrl), relativeUri).ToString();
                _ = this._httpClient.PostData(url, null);
            }
        }

        private void Update(string jobId, string taskId, HttpContent content)
        {
            string relativeUri = $"jobs/{jobId}/tasks/{taskId}";
            string url = new Uri(new Uri(_jobManagerUrl), relativeUri).ToString();
            _ = this._httpClient.PutData(url, content);
        }

        public void UpdateProgress(string jobId, string taskId, int progress)
        {
            using (var activity = this._activitySource.StartActivity("update task progress"))
            {
                // activity.AddTag("progress", progress);

                using (var content = new StringContent(JsonConvert.SerializeObject(new { percentage = progress },
                           this._jsonSerializerSettings)))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    Update(jobId, taskId, content);
                }
            }
        }

        public void UpdateCompletion(string jobId, string taskId, string? managerCallbackUrl)
        {
            using (this._activitySource.StartActivity("update task completed"))
            {
                using (var content = new StringContent(JsonConvert.SerializeObject(
                           new { percentage = 100, status = Status.COMPLETED }, this._jsonSerializerSettings)))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    Update(jobId, taskId, content);
                }
            }

            if (managerCallbackUrl is not null)
            {
            // Update overseer on task completion
            NotifyOnStatusChange(jobId, taskId, managerCallbackUrl);
            }
        }

        public void UpdateReject(string jobId, string taskId, int attempts, string reason, bool resettable, string? managerCallbackUrl)
        {
            using (var activity = this._activitySource.StartActivity("reject task"))
            {
                // activity.AddTag("attempts", attempts);
                // activity.AddTag("resettable", resettable);

                attempts++;

                // Check if the task should actually fail
                if (managerCallbackUrl is not null && (!resettable || attempts == this._maxAttempts))
                {
                    UpdateFailed(jobId, taskId, attempts, reason, resettable, managerCallbackUrl);
                    return;
                }

                using (var content = new StringContent(JsonConvert.SerializeObject(
                           new { status = Status.PENDING, attempts, reason, resettable },
                           this._jsonSerializerSettings)))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    Update(jobId, taskId, content);
                }
            }
        }

        private void UpdateFailed(string jobId, string taskId, int attempts, string reason, bool resettable, string? managerCallbackUrl)
        {
            using (var activity = this._activitySource.StartActivity("fail task"))
            {
                // activity.AddTag("reason", reason);

                using (var content = new StringContent(JsonConvert.SerializeObject(
                           new { status = Status.FAILED, attempts, reason, resettable }, this._jsonSerializerSettings)))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    Update(jobId, taskId, content);
                }
            }

            if (managerCallbackUrl is not null)
            {
            // Notify overseer on task failure
            NotifyOnStatusChange(jobId, taskId, managerCallbackUrl);
            }
        }
    }
}
