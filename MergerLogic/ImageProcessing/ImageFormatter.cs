using ImageMagick;
using System.Runtime.Serialization;

namespace MergerLogic.ImageProcessing
{
    public enum TileFormat
    {
        [EnumMember(Value = "png")] Png,
        [EnumMember(Value = "jpeg")] Jpeg,
    }

    public class TileFormatStrategy {
        public enum FormatStrategy {
            [EnumMember(Value = "fixed")] Fixed,
            [EnumMember(Value = "mixed")] Mixed,
        }

        private FormatStrategy _strategy;
        private TileFormat _format;

        public TileFormatStrategy(TileFormat format, FormatStrategy strategy = FormatStrategy.Fixed)
        {
            this._strategy = strategy;
            this._format = format;
        }

        public TileFormat ApplyStrategy(TileFormat format) {
            if (this._strategy == FormatStrategy.Fixed) {
                return this._format;
            }

            return format;
        }
    }

    public class ImageFormatter
    {
        public static byte[] ConvertToFormat(byte[] tile, TileFormat format)
        {
            TileFormat? currentFormat = GetTileFormat(tile);
            if (currentFormat != format)
            {
                using (var image = new MagickImage(tile))
                {
                    ConvertToFormat(image, format);
                    return image.ToByteArray();
                }
            }

            return tile;
        }

        public static void ConvertToFormat(IMagickImage image, TileFormat format)
        {
            switch (format)
            {
                case TileFormat.Jpeg:
                    image.Format = MagickFormat.Jpeg;
                    break;
                case TileFormat.Png:
                    image.Format = MagickFormat.Png32;
                    break;
            }
        }

        public static TileFormat? GetTileFormat(byte[] tile)
        {
            if (tile is null)
            {
                return null;
            }

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

        public static TileFormat? GetTileFormat(IMagickImage<byte> image) {
            if(image.IsOpaque) {
                image.Format = MagickFormat.Jpeg;
            }

            if (image.Format == MagickFormat.Jpg || image.Format == MagickFormat.Jpeg) {
                return TileFormat.Jpeg;
            }

            if (image.Format == MagickFormat.Png) {
                return TileFormat.Png;
            }

            return null;
        }

        public static void RemoveImageDateAttributes(IMagickImage? image)
        {
            if (image == null)
            {
                return;
            }

            image.RemoveAttribute("date:timestamp");
            image.RemoveAttribute("date:modify");
            image.RemoveAttribute("date:create");
        }
    }
}
