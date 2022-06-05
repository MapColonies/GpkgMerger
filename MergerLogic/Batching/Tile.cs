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

        public int BlobSize
        { get; private set; }

        public string Blob
        { get; private set; }

        public Tile(int z, int x, int y, string blob, int blobSize)
        {
            this.Z = z;
            this.X = x;
            this.Y = y;
            this.Blob = blob;
            this.BlobSize = blobSize;
        }

        public Tile(Coord coord, string blob, int blobSize)
        {
            this.Z = coord.z;
            this.X = coord.x;
            this.Y = coord.y;
            this.Blob = blob;
            this.BlobSize = blobSize;
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

        public void Print()
        {
            Console.WriteLine($"z: {this.Z}");
            Console.WriteLine($"x: {this.X}");
            Console.WriteLine($"y: {this.Y}");
            // Console.WriteLine($"blob: {this.Blob}");
            Console.WriteLine($"blobSize: {this.BlobSize}");
        }
    }
}
