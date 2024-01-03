using System.Reflection;

namespace MergerService.Runners
{
    public class MainRunner : IMainRunner
    {
        private readonly MergerLogic.Utils.IConfigurationManager _configurationManager;
        private readonly ILogger _logger;
        private readonly ITaskRunner _taskRunner;

        public MainRunner(MergerLogic.Utils.IConfigurationManager configurationManager, ILogger<MainRunner> logger, ITaskRunner taskRunner)
        {
            this._configurationManager = configurationManager;
            this._logger = logger;
            this._taskRunner = taskRunner;
        }

        public void Start()
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            this._logger.LogDebug($"[{methodName}] Start App");
            var pollingTime = this._configurationManager.GetConfiguration<int>("TASK", "pollingTime");

            var taskTypes = this._taskRunner.BuildTypeList();
            if (taskTypes.Count == 0)
            {
                string message = "No tasks configured, please provide job and task types";
                this._logger.LogCritical(message);
                throw new Exception(message);
            }

            this._logger.LogInformation($"[{methodName}] starting task polling loop");
            while (true)
            {
                bool activatedAny = false;
                foreach (var item in taskTypes)
                {
                    var task = this._taskRunner.FetchTask(item);
                    activatedAny = activatedAny || this._taskRunner.RunTask(task);
                }

                // Sleep only if there was no task to run for any type
                if (!activatedAny)
                {
                    // Sleep for 1 sec so we won't send too many requests if there are no tasks available
                    Thread.Sleep(pollingTime);
                }
            }
        }
    }
}
