using MergerLogic.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Reflection;
using System.Text;
using System.Timers;
using System.Net.Http.Json;

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
        private string? _taskId;

        public HeartbeatClient(ILogger<GpkgClient> logger, IConfigurationManager configurationManager, IHttpRequestUtils httpClient)
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

        ~HeartbeatClient()
        {
            this._timer.Elapsed -= this.Send;
            this._timer.Dispose();
        }

        public void Start(string taskId)
        {
            if (this._timer.Enabled)
            {
                this.Stop();
            }
            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] Starting heartbeat for task={taskId}");
            this._timer.Enabled = true;
            this._taskId = taskId;
        }

        public void Stop()
        {
            if (!this._timer.Enabled)
            {
                throw new Exception($"[{MethodBase.GetCurrentMethod().Name}] Heartbeat interval must be running in order to stop it.");
            }
            this._logger.LogInformation($"[{MethodBase.GetCurrentMethod().Name}] Stops heartbeats for taskId={this._taskId}");
            this._timer.Stop();
            try
            {
                string relativeUri = $"heartbeat/remove";
                string url = new Uri(new Uri(this._baseUrl), relativeUri).ToString();
                var content = JsonContent.Create(new[] { this._taskId });
                this._httpClient.PostData(url, content);
            }
            catch (Exception e)
            {
                string message = $"[{MethodBase.GetCurrentMethod().Name}] Could not delete heartbeat for task={this._taskId}, {e.Message}";
                this._logger.LogError(message);
                throw new Exception(message, e);
            }
            finally
            {
                this._taskId = null;
            }
        }

        public void Send(object? sender, ElapsedEventArgs elapsedEventArgs)
        {
            try
            {
                string relativeUri = $"heartbeat/{this._taskId}";
                string url = new Uri(new Uri(this._baseUrl), relativeUri).ToString();
                this._httpClient.PostData(url, null);
            }
            catch (Exception e)
            {
                this._logger.LogError($"[{MethodBase.GetCurrentMethod().Name}] Could not send heartbeat for task={this._taskId}, {e.Message}");
                throw;
            }
        }
    }
}