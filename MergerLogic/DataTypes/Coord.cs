using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class Coord
    {
        public int z;
        public int x;
        public int y;

        public Coord(int z, int x, int y)
        {
            this.z = z;
            this.x = x;
            this.y = y;
        }

        public void Print()
        {
            Console.WriteLine($"z: {this.z}");
            Console.WriteLine($"x: {this.x}");
            Console.WriteLine($"y: {this.y}");
        }
    }
}
