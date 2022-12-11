using MergerLogic.Clients;
using System.Diagnostics;
using MergerLogic.Utils;
using MergerService.Models.Jobs;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MergerService.Utils
{
    // TODO: rename all utils to clients
    public class JobUtils : IJobUtils
    {
        private readonly IHttpRequestUtils _httpClient;
        private readonly IConfigurationManager _configuration;
        private readonly ILogger _logger;
        private readonly IHeartbeatClient _heartbeatClient;
        private readonly ActivitySource _activitySource;
        private readonly JsonSerializerSettings _jsonSerializerSettings;
        private readonly string _jobManagerUrl;

        public JobUtils(IConfigurationManager configuration, IHttpRequestUtils httpClient, ILogger<JobUtils> logger,
            ActivitySource activitySource, IHeartbeatClient heartbeatClient)
        {
            this._httpClient = httpClient;
            this._configuration = configuration;
            this._logger = logger;
            this._heartbeatClient = heartbeatClient;
            this._activitySource = activitySource;

            // Construct Json serializer settings
            _jsonSerializerSettings = new JsonSerializerSettings();
            _jsonSerializerSettings.Converters.Add(new StringEnumConverter());

            _jobManagerUrl = this._configuration.GetConfiguration("TASK", "jobManagerUrl");
            //TODO: add tracing
        }

        public MergeJob? GetJob(string jobId)
        {
            using (this._activitySource.StartActivity("dequeue job"))
            {
                string relativeUri = $"jobs/{jobId}";
                string url = new Uri(new Uri(_jobManagerUrl), relativeUri).ToString();
                string? jobData = this._httpClient.GetDataString(url);
                if (jobData is null)
                {
                    this._logger.LogWarning($"Job id:{jobData}, not found");
                    return null;
                }

                try
                {
                    this._logger.LogDebug($"Found merge job data: {jobData}");
                    return JsonConvert.DeserializeObject<MergeJob>(jobData, this._jsonSerializerSettings)!;
                }
                catch (Exception e)
                {
                    this._logger.LogError(e.Message);
                    return null;
                }
            }
        }
    }
}
