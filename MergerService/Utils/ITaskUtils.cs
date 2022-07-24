using MergerService.Controllers;

namespace MergerService.Utils
{
    public interface ITaskUtils
    {
        MergeTask? GetTask(string jobType, string taskType);
    }
}
