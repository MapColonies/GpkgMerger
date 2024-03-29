namespace MergerLogic.DataTypes
{
    public class Coord
    {
        public int Z;
        public int X;
        public int Y;

        public Coord(int z, int x, int y)
        {
            this.Z = z;
            this.X = x;
            this.Y = y;
        }

        public void Print()
        {
            Console.WriteLine($"z: {this.Z}");
            Console.WriteLine($"x: {this.X}");
            Console.WriteLine($"y: {this.Y}");
        }

        public override bool Equals(object? obj)
        {
            return obj != null && obj.GetType() == typeof(Coord) && this.Equals((Coord)obj);
        }

        public bool Equals(Coord other)
        {
            return this.Z == other.Z && this.X == other.X && this.Y == other.Y;
        }
    }
}
