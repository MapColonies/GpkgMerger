using MergerLogic.DataTypes;
using MergerLogic.Utils;

namespace MergerLogic.Batching
{
    public delegate Tile CorrespondingTileBuilder();

    public class Tile
    {
        public int Z
        { get; private set; }

        public int X
        { get; private set; }

        public int Y
        { get; private set; }

        private byte[] _data;

        public Tile(int z, int x, int y, byte[] data)
        {
            this.Z = z;
            this.X = x;
            this.Y = y;
            this._data = data;
        }

        public Tile(Coord cords, byte[] data)
        {
            this.Z = cords.z;
            this.X = cords.x;
            this.Y = cords.y;
            this._data = data;
        }

        protected Tile(int z, int x, int y)
        {
            this.Z = z;
            this.X = x;
            this.Y = y;
        }

        public bool HasCoords(int z, int x, int y)
        {
            return z == this.Z && x == this.X && y == this.Y;
        }

        public Coord GetCoord()
        {
            return new Coord(this.Z, this.X, this.Y);
        }

        public void FlipY()
        {
            this.Y = GeoUtils.FlipY(this.Z, this.Y);
        }

        public virtual void Print()
        {
            Console.WriteLine($"z: {this.Z}");
            Console.WriteLine($"x: {this.X}");
            Console.WriteLine($"y: {this.Y}");
            // Console.WriteLine($"blob: {this.Blob}");
            Console.WriteLine($"data Size: {this._data.Length}");
        }

        public virtual byte[] GetImageBytes()
        {
            return this._data;
        }

        public void SetCoords(Coord cords)
        {
            this.X = cords.x;
            this.Y = cords.y;
            this.Z = cords.z;
        }

        public void SetCoords(int z, int x, int y)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public override string ToString()
        {
            return $"z: {this.Z}, x: {this.X}, y: {this.Y}, data size: {this._data.Length}";
        }
    }
}
