using MergerService.Controllers;

namespace MergerService.Utils
{
    public interface ITaskUtils
    {
        MergeTask? GetTask(string jobType, string taskType);

        void UpdateTask(string jobId, string taskId, UpdateParameters updateMetadata);

        public void UpdateCompletion(string jobId, string taskId);
    }
}
