using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public class TileMerger : ITileMerger
    {
        private readonly ITileScaler _tileScaler;
        private readonly IImageFormatter _imageFormatter;

        public TileMerger(ITileScaler tileScaler, IImageFormatter imageFormatter)
        {
            this._tileScaler = tileScaler;
            this._imageFormatter = imageFormatter;
        }

        public byte[]? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords,TileFormat format)
        {
            var images = this.GetImageList(tiles, targetCoords, out bool singleImage);
            byte[] data;
            switch (images.Count)
            {
                case 0:
                    // There are no images
                    return null;
                case 1:
                    this._imageFormatter.ConvertToFormat(images[0], format);
                    data = images[0].ToByteArray();
                    images[0].Dispose();
                    return data;
                default:
                    using (var imageCollection = new MagickImageCollection())
                    {
                        for (var i = images.Count - 1; i >= 0; i--)
                        {
                            imageCollection.Add(images[i]);
                        }

                        using (var mergedImage = imageCollection.Flatten(MagickColor.FromRgba(0, 0, 0, 0)))
                        {
                            mergedImage.ColorSpace = ColorSpace.sRGB;
                            mergedImage.ColorType = mergedImage.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
                            this._imageFormatter.ConvertToFormat(mergedImage, format);
                            var mergedImageBytes = mergedImage.ToByteArray();

                            for (var i = images.Count - 1; i >= 0; i--)
                            {
                                images[i].Dispose();
                            }

                            return mergedImageBytes;
                        }

                    }
            }
        }

        private List<MagickImage> GetImageList(List<CorrespondingTileBuilder> tiles, Coord targetCoords, out bool singleImage)
        {
            var images = new List<MagickImage>();
            int i = tiles.Count - 1;
            Tile? tile = null;

            singleImage = false;
            bool hasAlpha = false;
            try
            {
                tile = GetFirstTile(tiles, targetCoords, ref i);
                
                this.AddTileToImageList(targetCoords, tile, images, out hasAlpha);
                if (!hasAlpha)
                {
                    singleImage = true;
                    return images;
                }

                for (; i >= 0; i--)
                {
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

            singleImage = images.Count == 0 && tile != null;
            return images;
        }

        private Tile? GetFirstTile(List<CorrespondingTileBuilder> tiles, Coord targetCoords, ref int i)
        {
            for (; i >= 0; i--)
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
                hasAlpha = tileImage.HasAlpha;
                images.Add(tileImage);
            }
            else
            {
                hasAlpha = true;
            }
        }
    }
}
