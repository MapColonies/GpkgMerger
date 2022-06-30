using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public interface ITileScaler
    {
        void Upscale(MagickImage baseImage, Tile baseTile, Coord targetCoords);
    }
}
