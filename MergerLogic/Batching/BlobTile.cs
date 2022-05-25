using MergerLogic.Utils;

namespace MergerLogic.Batching
{
    public class BlobTile : Tile
    {
        public int BlobSize { get; private set; }

        public string Blob { get; private set; }

        public BlobTile(int z, int x, int y, string blob, int blobSize) : base(z, x, y)
        {
            this.Blob = blob;
            this.BlobSize = blobSize;
        }

        public override void Print()
        {
            Console.WriteLine($"z: {this.Z}");
            Console.WriteLine($"x: {this.X}");
            Console.WriteLine($"y: {this.Y}");
            // Console.WriteLine($"blob: {this.Blob}");
            Console.WriteLine($"data Size: {this.BlobSize}");
        }

        public override byte[] GetImageBytes()
        {
            return StringUtils.StringToByteArray(this.Blob);
        }

    }
}
