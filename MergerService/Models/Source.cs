namespace MergerService.Controllers
{
    public class Source
    {
        public string Path { get; }

        public string Type { get; }

        public string Origin { get; }

        public string Grid { get; }

        public Source(string path, string type, string origin = "UL", string grid = "2x1")
        {
            this.Path = path;
            this.Type = type;
            this.Origin = origin;
            this.Grid = grid;
        }
    }
}
