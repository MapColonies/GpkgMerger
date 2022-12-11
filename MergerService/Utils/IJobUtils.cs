using MergerService.Controllers;

namespace MergerService.Utils
{
    public interface IJobUtils
    {
        MergeJob? GetJob(string jobId);
    }
}
