using System;

namespace GpkgMerger.Src.Batching
{
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

        public bool HasCoords(int z, int x, int y)
        {
            return z == this.X && x == this.X && y == this.Y;
        }

        public void PrintTile()
        {
            Console.WriteLine($"z: {this.Z}");
            Console.WriteLine($"x: {this.X}");
            Console.WriteLine($"y: {this.Y}");
            // Console.WriteLine($"blob: {this.Blob}");
            Console.WriteLine($"blobSize: {this.BlobSize}");
        }
    }
}