using MergerLogic.Batching;
using MergerService.Utils;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MergerService.Controllers
{
    public class MergeTask
    {
        [JsonInclude]
        public TileBounds[]? Batches { get; }

        [JsonInclude]
        public Source[]? Sources { get; }

        public MergeTask(TileBounds[] batches, Source[] sources)
        {
            this.Batches = batches;
            this.Sources = sources;
        }

        public void Print()
        {
            if (this.Sources is null)
            {
                return;
            }

            Console.WriteLine("Sources:");
            foreach (Source source in this.Sources)
            {
                source.Print();
            }

            if (this.Batches is null)
            {
                return;
            }

            Console.WriteLine("Batches:");
            foreach (TileBounds bounds in this.Batches)
            {
                bounds.Print();
            }
        }

        public static MergeTask? GetTask(ILogger<MergeTask> logger)
        {
            string taskJson = TaskUtils.GetTask();

            try
            {
                return JsonSerializer.Deserialize<MergeTask>(taskJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            }
            catch (Exception e)
            {
                logger.LogError(e,$"failed to deserialize task: {e.Message}");
                return null;
            }
        }
    }
}
