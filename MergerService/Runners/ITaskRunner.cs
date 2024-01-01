using MergerService.Models.Tasks;
using MergerService.Utils;

namespace MergerService.Runners
{
  public interface ITaskRunner
  {
    List<KeyValuePair<string, string>> BuildTypeList();
    MergeTask? FetchTask(KeyValuePair<string, string> jobTaskTypesPair);
    bool RunTask(MergeTask? task);
  }
}
