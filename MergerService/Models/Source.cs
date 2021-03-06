using MergerLogic.DataTypes;

namespace MergerService.Controllers
{
    public class Source
    {
        public string Path { get; }

        public string Type { get; }

        public GridOrigin Origin { get; }

        public string Grid { get; }

        public Source(string path, string type, string origin = "UL", string grid = "2x1")
        {
            this.Path = path;
            this.Type = type;
            this.Origin = origin.ToLower() == "ul" ? GridOrigin.UPPER_LEFT : GridOrigin.LOWER_LEFT;
            this.Grid = grid;
        }

        public bool IsOneXOne()
        {
            return this.Grid.ToLower() == "1x1";
        }

        public void Print()
        {
            Console.WriteLine($"Path: {this.Path}");
            Console.WriteLine($"Type: {this.Type}");
            Console.WriteLine($"Origin: {this.Origin}");
            Console.WriteLine($"Grid: {this.Grid}");
        }
    }
}
