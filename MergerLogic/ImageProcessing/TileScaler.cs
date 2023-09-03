using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Monitoring.Metrics;
using MergerLogic.Utils;
using System.Diagnostics;

namespace MergerLogic.ImageProcessing
{
    public class TileScaler : ITileScaler
    {
        private const int TILE_SIZE = 256;
        private readonly IMetricsProvider _metricsProvider;

        public TileScaler(IMetricsProvider metricsProvider)
        {
            this._metricsProvider = metricsProvider;
        }

        public MagickImage? Upscale(MagickImage baseImage, Tile baseTile, Coord targetCoords)
        {
            var upscaleStopWatch = new Stopwatch();
            upscaleStopWatch.Start();

            // Calculate scale diff
            int zoomLevelDiff = targetCoords.Z - baseTile.Z;
            int scale = 1 << zoomLevelDiff;

            // Find source pixels range (flip Y direction as tiles are LL and pixels are UL)
            double tilePartX = targetCoords.X % scale;
            double tilePartY = scale - 1 - (targetCoords.Y % scale);

            /* Calculate dest tile sizes as seen in src zoom level.
            * The "shift" should be calculated according to the scale.
            *
            * Example:
            * Assume src_zoom = 3, dst_zoom = 13, TILE_SIZE = 256
            * scale = 2 ^ (13 - 3) = 2 ^ 10 = 1024
            * subTileShift = 256 / 1024 = 0.25
            *
            * The x and y pixles will be according to the calculated shift.
            */
            double subTileShift = (double)TILE_SIZE / scale;

            // Calculate start pixles in src
            int pixelX = (int)(tilePartX * subTileShift);
            int pixelY = (int)(tilePartY * subTileShift);

            MagickImage scaledImage;
            var colorSpace = baseImage.ColorSpace;
            var colorType = baseImage.ColorType;
            baseImage.ColorType = baseImage.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
            baseImage.ColorSpace = ColorSpace.sRGB;

            using (var srcPixels = baseImage.GetPixels())
            {
                /*
                * If the scale is bigger than the tile size, then that means that the dst image needs to be represented as one pixel in the src.
                * Calculation:
                * scale = 2 ^ (dst_zoom - src_zoom)
                * subTileShift = TILE_SIZE / scale
                *
                * Note that the scale is always in powers of 2 because of the ratio between zoom levels.
                */
                if (scale >= TILE_SIZE)
                {
                    var color = srcPixels.GetPixel(pixelX, pixelY).ToColor()!;
                    scaledImage = new MagickImage(color, TILE_SIZE, TILE_SIZE);
                }
                else
                {
                    int maxSrcX = pixelX + (int)subTileShift;
                    int maxSrcY = pixelY + (int)subTileShift;

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
            }

            scaledImage.Format = baseImage.Format;
            scaledImage.HasAlpha = baseImage.HasAlpha;
            scaledImage.ColorType = colorType;
            scaledImage.ColorSpace = colorSpace;

            upscaleStopWatch.Stop();
            this._metricsProvider.TotalTileUpscaleTimeHistogram()?.Observe(upscaleStopWatch.Elapsed.TotalSeconds);
            return ImageUtils.IsTransparent(scaledImage) ? null : scaledImage;

        }

        public Tile? Upscale(Tile tile, Coord targetCoords)
        {
            MagickImage? upscale;
            var tileBytes = tile.GetImageBytes();

            using (MagickImage tileImage = new MagickImage(tileBytes))
            {
                upscale = this.Upscale(tileImage, tile, targetCoords);
            }

            return upscale is null ? null : new Tile(targetCoords, upscale.ToByteArray());
        }
    }
}
