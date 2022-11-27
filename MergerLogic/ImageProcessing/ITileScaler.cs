using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public interface ITileScaler
    {
        MagickImage Upscale(MagickImage baseImage, Tile baseTile, Coord targetCoords);
    }
}
