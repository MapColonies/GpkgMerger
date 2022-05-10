using System;
using System.Collections.Generic;
using ImageMagick;
using GpkgMerger.Src.Batching;
using GpkgMerger.Src.Utils;
using GpkgMerger.Src.DataTypes;

namespace GpkgMerger.Src.ImageProccessing
{
    public static class Merge
    {

        public static string MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords)
        {
            Tile lastProccessedTile;
            var images = getLimageList(tiles, targetCoords, out lastProccessedTile);
            switch (images.Count)
            {
                case 0:
                    return null;
                case 1:
                    return lastProccessedTile?.Blob;
                default:
                    using (var imageCollection = new MagickImageCollection())
                    {
                        for (var i = images.Count - 1; i >= 0; i--)
                        {
                            imageCollection.Add(images[i]);
                        }
                        using (var mergedImage = imageCollection.Flatten())
                        {
                            var mergedImageBytes = mergedImage.ToByteArray();
                            var blob = Convert.ToHexString(mergedImageBytes);
                            return blob;
                        }
                    }
            }
        }

        private static List<MagickImage> getLimageList(List<CorrespondingTileBuilder> tiles, Coord targetCoords, out Tile lastProccessedTile)
        {
            var images = new List<MagickImage>();
            lastProccessedTile = null;
            for (var i = tiles.Count - 1; i >= 0; i--)
            {
                MagickImage tileImage = null;
                try
                {
                    var tile = tiles[i]();
                    if (tile == null)
                    {
                        continue;
                    }
                    lastProccessedTile = tile;
                    if (tile.Z > targetCoords.z)
                    {
                        throw new NotImplementedException("down scaling tiles is not supported");
                    }
                    var tileBytes = StringUtils.StringToByteArray(tile.Blob);
                    tileImage = new MagickImage(tileBytes);
                    if (tile.Z < targetCoords.z)
                    {
                        Upscaling.Upscale(tileImage, tile, targetCoords);
                    }
                    images.Add(tileImage);
                    if (!tileImage.HasAlpha)
                    {
                        break;
                    }
                }
                catch
                {
                    //prevent memory leak in case of any exception while handeling images
                    images.ForEach(image => image.Dispose());
                    if (tileImage != null)
                        tileImage.Dispose();
                    throw;
                }
            }
            return images;
        }
    }
}
