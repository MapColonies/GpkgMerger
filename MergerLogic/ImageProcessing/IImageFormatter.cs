using ImageMagick;
using MergerLogic.Batching;

namespace MergerLogic.ImageProcessing
{
    public interface IImageFormatter
    {
        Tile ToPng(Tile tile);
        byte[] ToPng(byte[] imageData);
        void ToPng(MagickImage image);
    }
}
