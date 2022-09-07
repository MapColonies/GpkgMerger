using MergerLogic.Utils;
using Microsoft.Extensions.Logging;
using System.Timers;


namespace MergerLogic.Clients
{
    public class HeartbeatClient : IHeartbeatClient
    {
        private readonly ILogger _logger;
        private readonly IConfigurationManager _configurationManager;
        private readonly IHttpRequestUtils _httpClient;
        private readonly System.Timers.Timer _timer;
        private readonly string _baseUrl;
        private readonly int _intervalMs;
        private string _taskId;
        
        public HeartbeatClient(ILogger<GpkgClient> logger, IConfigurationManager configurationManager, IHttpRequestUtils httpClient )
        {
            this._logger = logger;
            this._configurationManager = configurationManager;
            this._httpClient = httpClient;
            this._baseUrl = this._configurationManager.GetConfiguration("HEARTBEAT", "baseUrl");
            this._intervalMs = this._configurationManager.GetConfiguration<int>("HEARTBEAT", "intervalMs");
            this._timer = new System.Timers.Timer();
            this._timer.Interval = this._intervalMs;
            this._timer.Elapsed += this.Send;
        }

        public void Start(string taskId)
        {
            this._taskId = taskId;
            this._logger.LogInformation($"Starts heartbeats for taskId={taskId}");
            this._timer.Enabled = true;

        }

        public void Stop()
        {
            if (!this._timer.Enabled)
            {
                throw new Exception("Timer must be running in order to stop it.");
            }
            this._logger.LogInformation($"Stops heartbeats for taskId={this._taskId}");
            this._timer.Enabled = false;
        }

        private void Send(object? sender, ElapsedEventArgs elapsedEventArgs)
        {
            this._logger.LogDebug($"Sending heartbeat for taskId={this._taskId}");
            string relativeUri = $"heartbeat/{this._taskId}";
            string url = new Uri(new Uri(this._baseUrl), relativeUri).ToString();
            this._httpClient.PostData(url, null);
        }
    }
}
