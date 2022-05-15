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
            Z = z;
            X = x;
            Y = y;
            Blob = blob;
            BlobSize = blobSize;
        }

        public bool HasCoords(int z, int x, int y)
        {
            return z == Z && x == X && y == Y;
        }

        public Coord GetCoord()
        {
            return new Coord(Z, X, Y);
        }

        public void FlipY()
        {
            Y = GeoUtils.FlipY(Z, Y);
        }

        public void Print()
        {
            Console.WriteLine($"z: {Z}");
            Console.WriteLine($"x: {X}");
            Console.WriteLine($"y: {Y}");
            // Console.WriteLine($"blob: {this.Blob}");
            Console.WriteLine($"blobSize: {BlobSize}");
        }
    }
}
