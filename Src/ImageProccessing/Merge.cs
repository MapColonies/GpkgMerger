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
            string blob;
            byte[] newBytes = StringUtils.StringToByteArray(newTile.Blob);
            using (MagickImage newImage = new MagickImage(newBytes))
            {
                if (newImage.HasAlpha)
                {
                    byte[] baseBytes = StringUtils.StringToByteArray(baseTile.Blob);
                    using (MagickImage baseImage = new MagickImage(baseBytes))
                    {

                        if (baseTile.Z != newTile.Z)
                        {
                            Upscaling.Upscale(baseImage, baseTile, newTile);
                        }

                        baseImage.Composite(newImage, CompositeOperator.SrcOver);
                        byte[] baseByteArr = baseImage.ToByteArray();
                        blob = Convert.ToHexString(baseByteArr);
                    }
                }
                else
                {
                    blob = newTile.Blob;
                }
            }

            return blob;
        }
    }
}
