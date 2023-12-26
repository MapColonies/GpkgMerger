using MergerService.Models.Tasks;
using MergerService.Utils;

namespace MergerService.Runners
{
  public interface ITaskRunner
  {
    List<KeyValuePair<string, string>> BuildTypeList();
    bool FetchAndRunTasks(KeyValuePair<string, string> jobTaskTypesPair);
  }
}
