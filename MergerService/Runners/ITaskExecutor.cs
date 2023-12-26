using MergerService.Models.Tasks;
using MergerService.Utils;

namespace MergerService.Runners
{
  public interface ITaskExecutor
  {
    void ExecuteTask(MergeTask task, ITaskUtils taskUtils, string? managerCallbackUrl);
  }
}
