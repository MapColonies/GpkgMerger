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

        public void flipY()
        {
            y = GeoUtils.FlipY(z, y);
        }

        public void Print()
        {
            Console.WriteLine($"z: {z}");
            Console.WriteLine($"x: {x}");
            Console.WriteLine($"y: {y}");
        }
    }
}
