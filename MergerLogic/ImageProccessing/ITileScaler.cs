using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProccessing
{
    public interface ITileScaler
    {
        void Upscale(MagickImage baseImage, Tile baseTile, Coord targetCoords);
    }
}