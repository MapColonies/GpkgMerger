using MergerService.Controllers;
using MergerService.Models.Tasks;

namespace MergerService.Utils
{
    public interface ITaskUtils
    {
        MergeTask? GetTask(string jobType, string taskType);

        public void UpdateProgress(string jobId, string taskId, UpdateParams updateParams);

        public void UpdateCompletion(string jobId, string taskId, string? managerCallbackUrl);

        public void UpdateReject(string jobId, string taskId, int attempts, string reason, bool resettable, string? managerCallbackUrl);

        public int MaxAttempts { get; }
    }
}
