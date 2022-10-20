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
        private System.Timers.Timer _timer;
        private readonly string _baseUrl;
        private readonly int _intervalMs;
        private string _taskId;

        public HeartbeatClient(ILogger<GpkgClient> logger, IConfigurationManager configurationManager, IHttpRequestUtils httpClient)
        {
            this._logger = logger;
            this._configurationManager = configurationManager;
            this._httpClient = httpClient;
            this._baseUrl = this._configurationManager.GetConfiguration("HEARTBEAT", "baseUrl");
            this._intervalMs = this._configurationManager.GetConfiguration<int>("HEARTBEAT", "intervalMs");
        }

        public void Start(string taskId)
        {
            this._logger.LogInformation($"Starts heartbeats for task={taskId}");
            if (this._timer != null) {
                this.Stop();
            }
            this._timer = new System.Timers.Timer();
            this._timer.Enabled = true;
            this._taskId = taskId;
            this._timer.Interval = this._intervalMs;
            this._timer.Elapsed += this.Send;
        }

        public void Stop()
        {
            if (this._timer == null || !this._timer.Enabled)
            {
                throw new Exception("Heartbeat interval must be running in order to stop it.");
            }
            this._logger.LogInformation($"Stops heartbeats for taskId={this._taskId}");
            this._timer.Enabled = false;
            this._timer.Elapsed -= this.Send;
            this._timer.Stop();
            this._timer.Dispose();
            this._timer = null;
        }

        public
         void Send(object? sender, ElapsedEventArgs elapsedEventArgs)
        {
            try
            {
                this._logger.LogDebug($"Sending heartbeat for taskId={this._taskId}");
                string relativeUri = $"heartbeat/{this._taskId}";
                string url = new Uri(new Uri(this._baseUrl), relativeUri).ToString();
                this._httpClient.PostData(url, null);
            }
            catch (Exception e)
            {
                this._logger.LogError($"Could not send heartbeat for task={this._taskId}, {e.Message}");
                throw;
            }

        }
    }
}
