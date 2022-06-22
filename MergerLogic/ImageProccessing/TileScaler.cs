using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public class TileScaler : ITileScaler
    {
        private const int TILE_WIDTH = 256;
        private const int TILE_HEIGHT = 256;

        public void Upscale(MagickImage baseImage, Tile baseTile, Coord targetCoords)
        {
            int zoomLevelDiff = targetCoords.z - baseTile.Z;
            int scale = 1 << zoomLevelDiff;
            double scaleAsDouble = (double)scale;

            double tilePartX = (targetCoords.x % scale) / scaleAsDouble;
            double tilePartY = (targetCoords.y % scale) / scaleAsDouble;
            double tileSize = TILE_HEIGHT / scaleAsDouble;

            int pixleX = (int)(tilePartX * TILE_WIDTH);
            int pixleY = (int)(tilePartY * TILE_HEIGHT);
            int imageWidth = Math.Max((int)tileSize, 1);
            int imageHeight = Math.Max((int)tileSize, 1);

            MagickGeometry geometry = new MagickGeometry(pixleX, pixleY, imageWidth, imageHeight);
            baseImage.Crop(geometry);
            baseImage.RePage();
            baseImage.Resize(TILE_WIDTH, TILE_HEIGHT);
        }
    }
}
