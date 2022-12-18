using MergerService.Models.Jobs;

namespace MergerService.Utils
{
    public interface IJobUtils
    {
        MergeJob? GetJob(string jobId);
    }
}
