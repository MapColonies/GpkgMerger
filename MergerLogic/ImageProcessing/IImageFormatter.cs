using ImageMagick;
using MergerLogic.Batching;

namespace MergerLogic.ImageProcessing
{
    public interface IImageFormatter
    {
        public Tile CovertToFormat(Tile tile, TileFormat format);
        public void CovertToFormat(IMagickImage image, TileFormat format);
        public byte[] CovertToFormat(byte[] tile, TileFormat format);
    }
}
