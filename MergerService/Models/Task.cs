using MergerLogic.Batching;
using MergerService.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergerService.Controllers
{
    public class MergeTask
    {
        [JsonInclude]
        public Bounds[]? Batches { get; }

        [JsonInclude]
        public Source[]? Sources { get; }

        public MergeTask(Bounds[] batches, Source[] sources)
        {
            this.Batches = batches;
            this.Sources = sources;
        }

        public static MergeTask? GetTask()
        {
            string taskJson = TaskUtils.GetTask();

            try
            {
                return JsonSerializer.Deserialize<MergeTask>(taskJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
