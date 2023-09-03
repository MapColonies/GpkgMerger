using MergerLogic.Clients;
using MergerLogic.Monitoring.Metrics;
using System.Diagnostics;
using System.Net.Http.Headers;
using MergerLogic.Utils;
using MergerService.Controllers;
using MergerService.Models.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Text.Json;
using System.Reflection;

namespace MergerService.Utils
{
    // TODO: rename all utils to clients
    public class TaskUtils : ITaskUtils
    {
        private readonly IHttpRequestUtils _httpClient;
        private readonly IConfigurationManager _configuration;
        private readonly ILogger _logger;
        private readonly IHeartbeatClient _heartbeatClient;
        private readonly ActivitySource _activitySource;
        private readonly int _maxAttempts;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly string _jobManagerUrl;
        private readonly IMetricsProvider _metricsProvider;

        public TaskUtils(IConfigurationManager configuration, IHttpRequestUtils httpClient, ILogger<TaskUtils> logger,
            ActivitySource activitySource, IHeartbeatClient heartbeatClient, IMetricsProvider metricsProvider)
        {
            this._httpClient = httpClient;
            this._configuration = configuration;
            this._logger = logger;
            this._heartbeatClient = heartbeatClient;
            this._activitySource = activitySource;
            this._metricsProvider = metricsProvider;
            this._maxAttempts = this._configuration.GetConfiguration<int>("TASK", "maxAttempts");

            // Construct Json serializer settings
            _jsonSerializerSettings = new JsonSerializerSettings();
            _jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            this._jsonSerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            this._jsonSerializerSettings.NullValueHandling = NullValueHandling.Ignore;


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

                try
                {
                    return taskData is null
                        ? null
                        : JsonConvert.DeserializeObject<MergeTask>(taskData, this._jsonSerializerSettings)!;
                }
                catch (Exception e)
                {
                    this._logger.LogWarning(e,
                        $"[{MethodBase.GetCurrentMethod().Name}] Error deserializing returned task");
                    return null;
                }
            }
        }

        private void NotifyOnStatusChange(string jobId, string taskId, string managerCallbackUrl)
        {
            using (this._activitySource.StartActivity("notify Manager on task completion"))
            {
                // Notify Manager on task completion
                this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] Notifying Manager on completion, job: {jobId}, task: {taskId}");
                string relativeUri = $"jobs/{jobId}/{taskId}/completed";
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

        public void UpdateProgress(string jobId, string taskId, UpdateParams updateParams)
        {
            using (var activity = this._activitySource.StartActivity("update task progress"))
            {
                // activity.AddTag("progress", progress);

                using (var content = new StringContent(JsonConvert.SerializeObject(updateParams,
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
                // Update Manager on task completion
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
                if (!resettable || attempts == this._maxAttempts)
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
                // Notify Manager on task failure
                NotifyOnStatusChange(jobId, taskId, managerCallbackUrl);
            }
        }
    }
}
