using MergerLogic.Clients;
using MergerLogic.Monitoring.Metrics;
using MergerService.Models.Tasks;
using MergerService.Utils;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace MergerService.Runners
{
    public class TaskRunner : ITaskRunner
    {
        private readonly IHeartbeatClient _heartbeatClient;
        private readonly IMetricsProvider _metricsProvider;
        private readonly ITaskExecutor _taskExecutor;
        private readonly ITaskUtils _taskUtils;
        private readonly IJobUtils _jobUtils;
        private readonly ILogger _logger;
        private readonly MergerLogic.Utils.IConfigurationManager _configurationManager;
        private readonly int _maxTaskRetriesAttempts;

        public TaskRunner(ITaskExecutor taskExecutor, IJobUtils jobUtils, ILogger<TaskRunner> logger,
            ITaskUtils taskUtils, IHeartbeatClient heartbeatClient, IMetricsProvider metricsProvider,
            MergerLogic.Utils.IConfigurationManager configurationManager)
        {
            this._taskUtils = taskUtils;
            this._heartbeatClient = heartbeatClient;
            this._metricsProvider = metricsProvider;
            this._taskExecutor = taskExecutor;
            this._jobUtils = jobUtils;
            this._logger = logger;
            this._configurationManager = configurationManager;
            this._maxTaskRetriesAttempts = this._configurationManager.GetConfiguration<int>("TASK", "maxAttempts");
        }

        public List<KeyValuePair<string, string>> BuildTypeList()
        {
            var taskTypes = this._configurationManager.GetChildren("TASK", "types");

            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();

            foreach (var pair in taskTypes)
            {
                var jobType = pair.GetValue<string>("JobType");
                var taskType = pair.GetValue<string>("taskType");
                values.Add(new KeyValuePair<string, string>(jobType, taskType));
            }

            return values;
        }

        public MergeTask? FetchTask(KeyValuePair<string, string> jobTaskTypesPair)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;
            MergeTask? task;
            string jobType = jobTaskTypesPair.Key;
            string taskType = jobTaskTypesPair.Value;

            try
            {
                task = this._taskUtils.GetTask(jobType, taskType);
            }
            catch (Exception e)
            {
                if (e is HttpRequestException &&
                    ((HttpRequestException)e).StatusCode == HttpStatusCode.NotFound)
                {
                    this._logger.LogDebug($"[{methodName}] No task was found to work on...");
                    return null;
                }

                this._logger.LogError(e, $"[{methodName}] Error in MergerService start - get task: {e.Message}");
                return null;
            }

            return task;
        }

        public bool RunTask(MergeTask? task)
        {
            string methodName = MethodBase.GetCurrentMethod().Name;

            // Guard clause in case there are no batches or sources
            if (task == null)
            {
                return false;
            }

            this._logger.LogInformation($"[{methodName}] Run Task: jobId {task.JobId}, taskId {task.Id}");
            string? managerCallbackUrl = this._jobUtils.GetJob(task.JobId)?.Parameters.ManagerCallbackUrl;
            string log = managerCallbackUrl == null ? "managerCallbackUrl not provided as job parameter" : $"managerCallback url: {managerCallbackUrl}";
            this._logger.LogDebug($"[{methodName}]{log}");

            // check if needs to fail task that was released by task liberator and reached max attempts
            if (task.Attempts >= this._maxTaskRetriesAttempts)
            {
                try
                {
                    string reason = string.IsNullOrEmpty(task.Reason) ? $"Max attempts reached, current attempt is {task.Attempts}" : $"{task.Reason} and Max attempts reached with {task.Attempts} attempts";
                    this._logger.LogWarning($"[{methodName}] reject job because attemts count reached, jobId {task.JobId}, taskId {task.Id}, {reason}");
                    this._taskUtils.UpdateReject(task.JobId, task.Id, task.Attempts, reason, task.Resettable, managerCallbackUrl);
                }
                catch (Exception innerError)
                {
                    this._logger.LogError(innerError, $"[{methodName}] job {task.JobId}, task {task.Id} Error in MergerService while updating reject status due to max attemps reached with {task.Attempts}, update task failure: {innerError.Message}");
                }

                return false;
            }

            var totalTaskStopwatch = Stopwatch.StartNew();
            bool taskSucceed = false;

            try
            {
                this._heartbeatClient.Start(task.Id);
                this._taskExecutor.ExecuteTask(task, this._taskUtils, managerCallbackUrl);
                taskSucceed = true;
            }
            catch (Exception e)
            {
                this._logger.LogError(e, $"[{methodName}] Error in MergerService while running task {task.Id}, error: {e.Message}");

                try
                {
                    this._taskUtils.UpdateReject(task.JobId, task.Id, task.Attempts, e.Message, true, managerCallbackUrl);
                }
                catch (Exception innerError)
                {
                    this._logger.LogError(e, $"[{methodName}] Error in MergerService while updating reject status, RunTask catch block - update task failure: {innerError.Message}");
                }
            }
            finally
            {
                totalTaskStopwatch.Stop();
                this._metricsProvider.TaskExecutionTimeHistogram(totalTaskStopwatch.Elapsed.TotalSeconds, task.Type);
                this._heartbeatClient.Stop();
            }

            if (!taskSucceed)
            {
                return false;
            }

            try
            {
                this._taskUtils.UpdateCompletion(task.JobId, task.Id, managerCallbackUrl);
                this._logger.LogInformation($"[{methodName}] Completed task: jobId: {task.JobId}, taskId: {task.Id}");
            }
            catch (Exception e)
            {
                this._logger.LogError(e, $"[{methodName}] Error in MergerService start - update task completion: {e.Message}");
            }

            return true;
        }
    }
}
