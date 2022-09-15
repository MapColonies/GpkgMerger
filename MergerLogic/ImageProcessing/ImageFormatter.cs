using ImageMagick;
using MergerLogic.Batching;

namespace MergerLogic.ImageProcessing
{
    public enum TileFormat
    {
        Png,
        Jpeg,
    }
    public class ImageFormatter : IImageFormatter
    {
        public Tile CovertToFormat(Tile tile, TileFormat format)
        {
            var tileData = tile.GetImageBytes();
            var currentFormat = this.getTileFormat(tileData);
            if (currentFormat != format)
            {
                using (var image = new MagickImage(tileData))
                {
                    this.CovertToFormat(image,format);
                    return new Tile(tile.Z,tile.X,tile.Y,image.ToByteArray());
                }
            }

            return tile;
        }

        public byte[] CovertToFormat(byte[] tile, TileFormat format)
        {
            var currentFormat = this.getTileFormat(tile);
            if (currentFormat != format)
            {
                using (var image = new MagickImage(tile))
                {
                    this.CovertToFormat(image, format);
                    return image.ToByteArray();
                }
            }

            return tile;
        }

        public void CovertToFormat(IMagickImage image, TileFormat format)
        {
            switch (format)
            {
                case TileFormat.Jpeg:
                    image.Format = MagickFormat.Jpeg;
                    break;
                case TileFormat.Png:
                    image.Format = MagickFormat.Png;
                    break;
            }
        }

        private TileFormat? getTileFormat(byte[] tile)
        {
            //files magic values: https://en.wikipedia.org/wiki/List_of_file_signatures

            //check if magic is png
            if (tile[0] == 0x89 && tile[1] == 0x50 && tile[2] == 0x4E && tile[3] == 0x47 &&
                tile[4] == 0x0D && tile[5] == 0x0A && tile[6] == 0x1A && tile[7] == 0x0A)
            {
                return TileFormat.Png;
            }
            //check if magic is jpeg
            else if (tile[0] == 0xFF && tile[1] == 0xD8 && tile[2] == 0xFF && tile[3] == 0xDB)
            {
                return TileFormat.Jpeg;
            }
            else if (tile[0] == 0xFF && tile[1] == 0xD8 && tile[2] == 0xFF && tile[3] == 0xE0)
            {
                return TileFormat.Jpeg;
            }
            else if (tile[0] == 0xFF && tile[1] == 0xD8 && tile[2] == 0xFF && tile[3] == 0xEE)
            {
                return TileFormat.Jpeg;
            }
            else if (tile[0] == 0xFF && tile[1] == 0xD8 && tile[2] == 0xFF && tile[3] == 0xE1 &&
                     tile[6] == 0x45 && tile[7] == 0x78 && tile[8] == 0x69 && tile[9] == 0x66 &&
                     tile[10] == 0x00 && tile[11] == 0x00)
            {
                return TileFormat.Jpeg;
            }
            
            
            return null;
        }
    }
}
