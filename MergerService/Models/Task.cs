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
    }
}
