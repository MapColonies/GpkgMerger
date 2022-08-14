using MergerService.Controllers;

namespace MergerService.Utils
{
    public interface ITaskUtils
    {
        MergeTask? GetTask(string jobType, string taskType);

        public void NotifyOnCompletion(string jobId, string taskId);

        public void UpdateProgress(string jobId, string taskId, int progress);

        public void UpdateCompletion(string jobId, string taskId);

        public void UpdateReject(string jobId, string taskId, int attempts, string reason, bool resettable);
    }
}
