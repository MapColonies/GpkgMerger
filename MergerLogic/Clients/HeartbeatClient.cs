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
            Console.WriteLine("DANI");
            this._logger.LogInformation($"Starts heartbeats for task");
            Console.WriteLine("TIMER:");
            this._timer = new System.Timers.Timer();
            this._timer.Enabled = true;
            Console.WriteLine("AFTER");
            this._taskId = taskId;
            
            this._timer.Interval = this._intervalMs;
            this._timer.Elapsed += this.Send;
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

        public
         void Send(object? sender, ElapsedEventArgs elapsedEventArgs)
        {
            this._logger.LogDebug($"Sending heartbeat for taskId={this._taskId}");
            string relativeUri = $"heartbeat/{this._taskId}";
            string url = new Uri(new Uri(this._baseUrl), relativeUri).ToString();
            this._httpClient.PostData(url, null);
        }
    }
}
