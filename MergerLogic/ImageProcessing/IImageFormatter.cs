using ImageMagick;

namespace MergerLogic.ImageProcessing
{
    public interface IImageFormatter
    {
        public void ConvertToFormat(IMagickImage image, TileFormat format);
        public byte[] ConvertToFormat(byte[] tile, TileFormat format);
        public TileFormat? GetTileFormat(byte[] tile);
    }
}
