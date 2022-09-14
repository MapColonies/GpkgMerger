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

        public byte[]? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords)
        {
            var images = this.GetImageList(tiles, targetCoords, out Tile? lastProcessedTile, out bool singleImage);
            byte[] data;
            switch (images.Count)
            {
                case 0:
                    // There are no images
                    if (!singleImage)
                    {
                        return null;
                    }

                    // Otherwise there is one image that wasn't loaded
                    data = lastProcessedTile!.GetImageBytes();
                    //check if magic is png
                    if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
                        data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
                    {
                        return data;
                    }

                    return this._imageFormatter.ToPng(data);
                case 1:
                    if (images[0].Format != MagickFormat.Png && images[0].Format != MagickFormat.Png8 &&
                        images[0].Format != MagickFormat.Png00 && images[0].Format != MagickFormat.Png24 &&
                        images[0].Format != MagickFormat.Png32 && images[0].Format != MagickFormat.Png48 &&
                        images[0].Format != MagickFormat.Png64)
                    {
                        this._imageFormatter.ToPng(images[0]);
                    }

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
                            this._imageFormatter.ToPng(mergedImage);
                            mergedImage.ColorSpace = ColorSpace.sRGB;
                            mergedImage.ColorType = mergedImage.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
                            var mergedImageBytes = mergedImage.ToByteArray();
                            return mergedImageBytes;
                        }
                    }
            }
        }

        private List<MagickImage> GetImageList(List<CorrespondingTileBuilder> tiles, Coord targetCoords,
            out Tile? lastProcessedTile, out bool singleImage)
        {
            var images = new List<MagickImage>();
            lastProcessedTile = null;
            int i = tiles.Count - 1;
            Tile? tile = null;

            singleImage = false;
            MagickImage? tileImage = null;
            try
            {
                tile = GetFirstTile(tiles, ref lastProcessedTile, ref i);
                for (; i >= 0; i--)
                {
                    var tile2 = tiles[i]();
                    if (tile2 == null)
                    {
                        continue;
                    }

                    this.AddTileToImageList(targetCoords, tile, images, ref tileImage);
                    if (!tileImage!.HasAlpha)
                    {
                        singleImage = true;
                        return images;
                    }

                    lastProcessedTile = tile2;
                    this.AddTileToImageList(targetCoords, tile2, images, ref tileImage);
                    if (!tileImage!.HasAlpha)
                    {
                        return images;
                    }

                    i--;
                    break;
                }

                for (; i >= 0; i--)
                {
                    tile = tiles[i]();
                    if (tile == null)
                    {
                        continue;
                    }

                    lastProcessedTile = tile;
                    this.AddTileToImageList(targetCoords, tile, images, ref tileImage);
                    if (!tileImage!.HasAlpha)
                    {
                        return images;
                    }
                }
            }
            catch
            {
                //prevent memory leak in case of any exception while handling images
                images.ForEach(image => image.Dispose());
                if (tileImage != null)
                    tileImage.Dispose();
                throw;
            }

            singleImage = images.Count == 0 && tile != null;
            return images;
        }

        private Tile? GetFirstTile(List<CorrespondingTileBuilder> tiles, ref Tile? lastProcessedTile, ref int i)
        {
            for (; i >= 0; i--)
            {
                Tile tile = tiles[i]();
                if (tile != null)
                {
                    lastProcessedTile = tile;
                    i--;
                    return tile;
                }
            }

            return null;
        }

        private void AddTileToImageList(Coord targetCoords, Tile? tile, List<MagickImage> images,
            ref MagickImage? tileImage)
        {
            if (tile!.Z > targetCoords.Z)
            {
                throw new NotImplementedException("down scaling tiles is not supported");
            }

            var tileBytes = tile.GetImageBytes();
            tileImage = new MagickImage(tileBytes);
            if (tile.Z < targetCoords.Z)
            {
                var upscale = this._tileScaler.Upscale(tileImage, tile, targetCoords);
                tileImage.Dispose();
                tileImage = upscale;
            }

            images.Add(tileImage);
        }
    }
}
