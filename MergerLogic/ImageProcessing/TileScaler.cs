using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public class TileScaler : ITileScaler
    {
        private const int TILE_SIZE = 256;

        public MagickImage Upscale(MagickImage baseImage, Tile baseTile, Coord targetCoords)
        {
            int zoomLevelDiff = targetCoords.Z - baseTile.Z;
            int scale = 1 << zoomLevelDiff;

            //find source pixels range
            double tilePartX = targetCoords.X % scale;
            double tilePartY = scale - 1 - (targetCoords.Y % scale);// flip direction as tiles are LL and pixels are UL
            int subTileSize = TILE_SIZE / scale;

            int pixelX = (int)(tilePartX * subTileSize);
            int pixelY = (int)(tilePartY * subTileSize);

            var srcPixels = baseImage.GetPixels();
            MagickImage scaledImage;
            var colorSpace = baseImage.ColorSpace;
            var colorType = baseImage.ColorType;
            baseImage.ColorType = baseImage.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor; 
            baseImage.ColorSpace = ColorSpace.sRGB;
            if (scale >= TILE_SIZE)
            {
                var color = srcPixels.GetPixel(pixelX, pixelY).ToColor()!;
                scaledImage = new MagickImage(color,TILE_SIZE,TILE_SIZE);
            }
            else
            {
                int maxSrcX = pixelX + subTileSize;
                int maxSrcY = pixelY + subTileSize;

                int channels = baseImage.ChannelCount;
                int byteCount = channels * TILE_SIZE * TILE_SIZE;
                var pixels = new byte[byteCount];

                int scaledChannels = scale * channels;
                int maxRowOffset = TILE_SIZE * scaledChannels;
                int pixelRowBytes = TILE_SIZE * channels;

                //loop relevant source pixels
                for (int i = pixelX; i < maxSrcX; i++)
                {
                    for (int j = pixelY; j < maxSrcY; j++)
                    {
                        var srcPixel = srcPixels.GetValue(i, j);
                        var targetXStart = (i - pixelX) * scale;
                        var targetYStart = (j - pixelY) * scale;
                        var targetPixelIdxStart = (targetXStart + (TILE_SIZE * targetYStart)) * channels;
                        for (int pixelColOffset = 0; pixelColOffset < scaledChannels; pixelColOffset += channels)
                        {
                            for (int pixelRowOffset = 0; pixelRowOffset < maxRowOffset; pixelRowOffset += pixelRowBytes)
                            {
                                int pixelIdx = targetPixelIdxStart + pixelColOffset + pixelRowOffset;
                                srcPixel!.CopyTo(pixels, pixelIdx); //copy all channels 
                            }
                        }
                    }
                }

                var set = new PixelReadSettings(TILE_SIZE, TILE_SIZE, StorageType.Char,
                    channels == 4 ? PixelMapping.RGBA : PixelMapping.RGB);
                scaledImage = new MagickImage(pixels, set);
            }

            scaledImage.Format = baseImage.Format;
            scaledImage.HasAlpha = baseImage.HasAlpha;
            scaledImage.ColorType = colorType;
            scaledImage.ColorSpace = colorSpace;
            return scaledImage;
        }
    }
}
