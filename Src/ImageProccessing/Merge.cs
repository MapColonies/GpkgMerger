using System;
using ImageMagick;
using GpkgMerger.Src.Batching;
using GpkgMerger.Src.Utils;

namespace GpkgMerger.Src.ImageProccessing
{
    public static class Merge
    {
        public static string MergeNewToBase(Tile newTile, Tile baseTile)
        {
            byte[] newBytes = StringUtils.StringToByteArray(newTile.Blob);
            MagickImage newImage = new MagickImage(newBytes);

            string blob;
            if (newImage.HasAlpha)
            {
                byte[] baseBytes = StringUtils.StringToByteArray(baseTile.Blob);
                MagickImage baseImage = new MagickImage(baseBytes);
                // blob = MergeWands(baseImage, newImage);
                baseImage.Composite(newImage, CompositeOperator.SrcOver);
                byte[] baseByteArr = baseImage.ToByteArray();
                blob = Convert.ToHexString(baseByteArr);
                // blob = baseImage.ToString();
            }
            else
            {
                blob = newTile.Blob;
            }

            return blob;
        }

        // public static string MergeWands(MagickImage baseImage, MagickImage newImage)
        // {

        // }
    }
}