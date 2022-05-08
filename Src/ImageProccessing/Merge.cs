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
            var images = new List<MagickImage>();
            Tile lastProccessedTile = null;
            for (var i = tiles.Count - 1; i >= 0; i--)
            {
                MagickImage tileImage = null;
                try
                {
                    lastProccessedTile = tiles[i]();
                    if (lastProccessedTile.Z > targetCoords.z)
                    {
                        images.ForEach(image => image.Dispose());
                        throw new NotImplementedException("down scaling tiles is not supported");
                    }
                    var tileBytes = StringUtils.StringToByteArray(lastProccessedTile.Blob);
                    tileImage = new MagickImage(tileBytes);
                    if (lastProccessedTile.Z < targetCoords.z)
                    {
                        Upscaling.Upscale(tileImage, lastProccessedTile, targetCoords);
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
            switch (images.Count)
            {
                case 0:
                    return null;
                case 1:
                    return lastProccessedTile.Blob;
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

    }
}
