using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProccessing
{
    public static class Upscaling
    {
        private const int TileWidth = 256;
        private const int TileHeight = 256;

        public static void Upscale(MagickImage baseImage, Tile baseTile, Coord targetCoords)
        {
            int zoomLevelDiff = targetCoords.z - baseTile.Z;
            int scale = 1 << zoomLevelDiff;
            double scaleAsDouble = (double)scale;

            double tilePartX = (targetCoords.x % scale) / scaleAsDouble;
            double tilePartY = (targetCoords.y % scale) / scaleAsDouble;
            double tileSize = TileHeight / scaleAsDouble;

            int pixleX = (int)(tilePartX * TileWidth);
            int pixleY = (int)(tilePartY * TileHeight);
            int imageWidth = Math.Max((int)tileSize, 1);
            int imageHeight = Math.Max((int)tileSize, 1);

            MagickGeometry geometry = new MagickGeometry(pixleX, pixleY, imageWidth, imageHeight);
            baseImage.Crop(geometry);
            baseImage.RePage();
            baseImage.Resize(TileWidth, TileHeight);
        }
    }
}
