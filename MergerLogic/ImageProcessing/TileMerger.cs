using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MergerLogic.ImageProcessing
{
    public class TileMerger : ITileMerger
    {
        private readonly ITileScaler _tileScaler;
        private readonly ILogger<TileMerger> _logger;

        public TileMerger(ITileScaler tileScaler, ILogger<TileMerger> logger)
        {
            this._logger = logger;
            this._tileScaler = tileScaler;
        }

        public Tile? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords, TileFormatStrategy strategy)
        {
            var images = this.GetImageList(tiles, targetCoords);
            IMagickImage<byte> image;

            switch (images.Count)
            {
                case 0:
                    // There are no images
                    this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] No images where found return null");
                    return null;
                case 1:
                    ImageFormatter.RemoveImageDateAttributes(images[0]);
                    image = images[0];
                    this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] 1 image found");
                    break;
                default:
                    using (var imageCollection = new MagickImageCollection())
                    {
                        for (var i = images.Count - 1; i >= 0; i--)
                        {
                            imageCollection.Add(images[i]);
                        }

                        this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] {imageCollection.Count} where found for merge, start 'imageMagic' merging");
                        using (var mergedImage = imageCollection.Flatten(MagickColor.FromRgba(0, 0, 0, 0)))
                        {
                            ImageFormatter.RemoveImageDateAttributes(mergedImage);

                            mergedImage.ColorSpace = ColorSpace.sRGB;
                            mergedImage.ColorType = mergedImage.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
                            image = new MagickImage(mergedImage);
                            this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] 'imageMagic' merging finished");
                        }
                    }
                    break;
            }

            Tile tile = new Tile(targetCoords, image);
            image.Dispose();
            tile.ConvertToFormat(strategy.ApplyStrategy(tile.Format));
            return tile;
        }

        private List<MagickImage> GetImageList(List<CorrespondingTileBuilder> tiles, Coord targetCoords)
        {
            var images = new List<MagickImage>();
            int i = tiles.Count - 1;
            Tile? tile = null;

            bool hasAlpha = false;
            try
            {
                tile = GetFirstTile(tiles, targetCoords, ref i);
                string message = tile == null ? "null" : $"z:{tile.Z}, x:{tile.X}, y:{tile.Y}";
                this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] first tile is {message}");
                this.AddTileToImageList(targetCoords, tile, images, out hasAlpha);
                if (!hasAlpha)
                {
                    return images;
                }

                for (; i >= 0; i--)
                {
                    // protect in case all "sources" tiles are null
                    if (images.Count == 0 && i == 0)
                    {
                        this._logger.LogDebug($"[{MethodBase.GetCurrentMethod().Name}] All sources are empty - return");
                        return images;
                    }

                    tile = tiles[i]();
                    if (tile is null)
                    {
                        continue;
                    }

                    this.AddTileToImageList(targetCoords, tile, images, out hasAlpha);
                    if (!hasAlpha)
                    {
                        return images;
                    }
                }
            }
            catch
            {
                //prevent memory leak in case of any exception while handling images
                images.ForEach(image => image.Dispose());
                throw;
            }

            return images;
        }

        private Tile? GetFirstTile(List<CorrespondingTileBuilder> tiles, Coord targetCoords, ref int i)
        {
            // We only need to go over sources, target should be excluded (e.g. i > 0 and not i >= 0)
            for (; i > 0; i--)
            {
                Tile? tile = tiles[i]();
                if (tile != null)
                {
                    // Check if should upscale tile (first tile does not upscale if there are no other tiles)
                    // TODO: refactor code in this class
                    if (tile.Z < targetCoords.Z)
                    {
                        tile = this._tileScaler.Upscale(tile, targetCoords);
                    }

                    i--;
                    return tile;
                }
            }

            return null;
        }

        private void AddTileToImageList(Coord targetCoords, Tile? tile, List<MagickImage> images, out bool hasAlpha)
        {
            if (tile is null)
            {
                hasAlpha = true;
                return;
            }

            if (tile!.Z > targetCoords.Z)
            {
                throw new NotImplementedException("down scaling tiles is not supported");
            }

            var tileBytes = tile.GetImageBytes();
            MagickImage? tileImage = new MagickImage(tileBytes);
            if (tile.Z < targetCoords.Z)
            {
                var upscale = this._tileScaler.Upscale(tileImage, tile, targetCoords);
                tileImage.Dispose();
                tileImage = upscale;
            }

            if (tileImage is not null)
            {
                hasAlpha = ImageUtils.IsTransparent(tileImage);
                images.Add(tileImage);
            }
            else
            {
                hasAlpha = true;
            }
        }
    }
}
