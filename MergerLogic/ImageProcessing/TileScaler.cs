using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public class TileScaler : ITileScaler
    {
        private const int TILE_SIZE = 256;

        public MagickImage? Upscale(MagickImage baseImage, Tile baseTile, Coord targetCoords)
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
            // TODO: return null only when the one color is black or white
            return scaledImage.TotalColors > 1 ? scaledImage : null;
        }
        
        private static bool IsWhite(MagickImage image)
        {
            MagickColor white = MagickColors.White;
            using var pixels = image.GetPixels();
            return !pixels.Select(pixel => pixel.ToColor()).Any(color => (MagickColor)color != white);
        }
        
        private static bool IsFullyTransparent(MagickImage image)
        {
            using var pixels = image.GetPixels();
            return !pixels.Select(pixel => pixel.ToColor()).Any(color => color.A != 255);
        }

        public MagickImage UpscaleFix(MagickImage baseImage, Tile baseTile, Coord targetCoords)
        {
            // Calculate scale diff
            int zoomLevelDiff = targetCoords.Z - baseTile.Z;
            int scale = 1 << zoomLevelDiff;

            // Find source pixels range (flip Y direction as tiles are LL and pixels are UL)
            double tilePartX = targetCoords.X % scale;
            double tilePartY = scale - 1 - (targetCoords.Y % scale);

            // Calculate dest tile sizes as seen in src zoom level
            int subTileSize = TILE_SIZE / scale;

            // Calculate start pixles in src
            int pixelX = (int)(tilePartX * subTileSize);
            int pixelY = (int)(tilePartY * subTileSize);

            var srcPixels = baseImage.GetPixels();
            MagickImage scaledImage;
            var colorSpace = baseImage.ColorSpace;
            var colorType = baseImage.ColorType;
            baseImage.ColorType = baseImage.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor; 
            baseImage.ColorSpace = ColorSpace.sRGB;

            /*
            * If the scale is bigger than the tile size, then that means that the dst image needs to be represented as one pixel in the src.
            * Calculation:
            * scale = 2 ^ (dst_zoom - src_zoom)
            * subTileSize = TILE_SIZE / scale
            *
            * Note that the scale is always in powers of 2 because of the ratio between zoom levels.
            *
            * Proof:
            * Assume scale > TILE_SIZE, then:
            * subTileSize = TILE_SIZE / scale > TILE_SIZE => TILE_SIZE > TILE_SIZE * scale => 1 > scale
            *
            * Example:
            * Assume src_zoom = 3, dst_zoom = 13, TILE_SIZE = 256
            * scale = 2 ^ (13 - 3) = 2 ^ 10 = 1024
            * subTileSize = 256 / 1024 = 0.25
            * We can see that for this example, any difference in 8 zoom levels or more will result in a resolution smaller than a pixel (0.25) in the src.
            * We round up to a pixel and create the dst image as that pixels color.
            */
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
