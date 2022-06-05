using MergerLogic.DataTypes;

namespace MergerService.Controllers
{
    public class Source
    {
        public string Path { get; }

        public string Type { get; }

        public Origin Origin { get; }

        public string Grid { get; }

        public Source(string path, string type, string origin = "UL", string grid = "2x1")
        {
            this.Path = path;
            this.Type = type;
            this.Origin = origin.ToLower() == "ul" ? Origin.UL : Origin.LL;
            this.Grid = grid;
        }

        public bool IsOneXOne()
        {
            return Grid.ToLower() == "1x1";
        }
    }
}
