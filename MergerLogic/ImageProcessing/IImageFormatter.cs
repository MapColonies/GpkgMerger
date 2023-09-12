using ImageMagick;
using MergerLogic.Batching;

namespace MergerLogic.ImageProcessing
{
    public interface IImageFormatter
    {
        public Tile ConvertToFormat(Tile tile, TileFormat format);
        public void ConvertToFormat(IMagickImage image, TileFormat format);
        public byte[] ConvertToFormat(byte[] tile, TileFormat format);
        public TileFormat GetTileFormat(byte[] tile);
    }
}
