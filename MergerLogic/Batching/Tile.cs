using ImageMagick;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using System.ComponentModel.DataAnnotations;

namespace MergerLogic.Batching
{
    public delegate Tile? CorrespondingTileBuilder();

    public class Tile
    {
        public int Z
        {
            get;
            private set;
        }

        public int X
        {
            get;
            private set;
        }

        public int Y
        {
            get;
            internal set;
        }

        public TileFormat Format { get; internal set; }

        private byte[] _data;

        public Tile(int z, int x, int y, byte[] data)
        {
            this.Z = z;
            this.X = x;
            this.Y = y;
            this.Format = ImageFormatter.GetTileFormat(data) ?? throw new ValidationException($"Cannot create tile {this}, data is in invalid format");
            this._data = data;
        }

        public Tile(Coord cords, byte[] data)
        {
            this.Z = cords.Z;
            this.X = cords.X;
            this.Y = cords.Y;
            this.Format = ImageFormatter.GetTileFormat(data) ?? throw new ValidationException($"Cannot create tile {this}, data is in invalid format");
            this._data = data;
        }

        public Tile(Coord cords, IMagickImage<byte> image)
        {
            this.Z = cords.Z;
            this.X = cords.X;
            this.Y = cords.Y;
            this.Format = ImageFormatter.GetTileFormat(image) ?? throw new ValidationException($"Cannot create tile {this}, data is in invalid format");
            this._data = image.ToByteArray();
        }

        public bool HasCoords(int z, int x, int y)
        {
            return z == this.Z && x == this.X && y == this.Y;
        }

        public Coord GetCoord()
        {
            return new Coord(this.Z, this.X, this.Y);
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

        public int Size() {
            return this._data.Length;
        }

        public void ConvertToFormat(TileFormat format)
        {
            this._data = ImageFormatter.ConvertToFormat(this._data, format);
            this.Format = format;
        }

        public void SetCoords(Coord cords)
        {
            this.X = cords.X;
            this.Y = cords.Y;
            this.Z = cords.Z;
        }

        public void SetCoords(int z, int x, int y)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public override string ToString()
        {
            int dataSize = this._data?.Length ?? 0;
            return $"z: {this.Z}, x: {this.X}, y: {this.Y}, data size: {dataSize}";
        }
    }
}
