using ImageMagick;
using GpkgMerger.Src.Batching;
using System;

namespace GpkgMerger.Src.ImageProccessing
{
    public static class Upscaling
    {
        private const int TileWidth = 256;
        private const int TileHeight = 256;

        public static void Upscale(MagickImage baseImage, Tile baseTile, Tile newTile)
        {
            int zoomLevelDiff = newTile.Z - baseTile.Z;
            int scale = 1 << zoomLevelDiff;
            double scaleAsDouble = (double)scale;

            double tilePartX = (newTile.X % scale) / scaleAsDouble;
            double tilePartY = (newTile.Y % scale) / scaleAsDouble;
            double tileSize = TileHeight / scaleAsDouble;

            int pixleX = (int)(tilePartX * TileWidth);
            int pixleY = (int)(tilePartY * TileHeight);
            int imageWidth = Math.Max((int)tileSize, 1);
            int imageHeight = Math.Max((int)tileSize, 1);

            MagickGeometry geometry = new MagickGeometry(pixleX, pixleY, imageWidth, imageHeight);
            if (baseTile.HasCoords(16, 78023, 44133))
            {
                baseTile.Print();
                newTile.Print();
                Console.WriteLine($"{pixleX}, {pixleY}, {imageWidth}, {imageHeight}");
                Console.WriteLine($"image: {baseImage}, geometry: {geometry}");
            }
            baseImage.Crop(geometry);
            baseImage.Resize(TileWidth, TileHeight);
        }
    }
}
