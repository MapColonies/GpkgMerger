using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public class TileScaler : ITileScaler
    {
        private const int TILE_WIDTH = 256;
        private const int TILE_HEIGHT = 256;

        public MagickImage Upscale(MagickImage baseImage, Tile baseTile, Coord targetCoords)
        {
            int zoomLevelDiff = targetCoords.Z - baseTile.Z;
            int scale = 1 << zoomLevelDiff;

            //find source pixels range
            double tilePartX = targetCoords.X % scale;
            double tilePartY = targetCoords.Y % scale;
            double tileSize = TILE_HEIGHT / (double)scale;

            int pixelX = (int)(tilePartX * tileSize);
            int pixelY = (int)(tilePartY * tileSize);
            int srcSize = Math.Max((int)tileSize, 1);
            int maxSrcX = pixelX + srcSize;
            int maxSrcY = pixelY + srcSize;

            //prepare pixels data 
            var scaledImage = new MagickImage(MagickColor.FromRgba(0, 0, 0, 0), TILE_WIDTH, TILE_HEIGHT);
            scaledImage.HasAlpha = baseImage.HasAlpha;
            var srcPixels = baseImage.GetPixels();
            var targetPixels = scaledImage.GetPixels();
            int byteCount = 4 * scale * scale;
            var pixels = new byte[byteCount];

            //loop relevant source pixels
            for (int i = pixelX; i < maxSrcX; i++)
            {
                for (int j = pixelY; j < maxSrcY; j++)
                {
                    var srcPixel = srcPixels.GetValue(i, j);
                    //copy only opaque pixels
                    if (srcPixel![3] != 0)
                    {
                        //create new pixel data by duplicating source pixel data
                        for (int k = 0; k < byteCount; k += 4)
                        {
                            srcPixel!.CopyTo(pixels, k); //copy all 4 channels 
                        }

                        //update target pixels
                        targetPixels.SetArea((i - pixelX) * scale, (j - pixelY) * scale, scale, scale, pixels);
                    }
                }
            }

            return scaledImage;
        }
    }
}
