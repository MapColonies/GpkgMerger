using System;
using System.Collections.Generic;
using ImageMagick;
using GpkgMerger.Src.Batching;
using GpkgMerger.Src.Utils;

namespace GpkgMerger.Src.ImageProccessing
{
    public static class Merge
    { 
        // public static string MergeTiles(IEnumerable<Tile> tiles)
        // {
        //     using (var images = new MagickImageCollection())
        //     {
        //         using (var iterator = tiles.GetEnumerator())
        //         {
        //             if (!iterator.MoveNext())
        //             {
        //                 //no tiles recieved
        //                 return null;  //TODO: replace with proper error
        //             }
        //             var firstTile = iterator.Current;
        //             var firstTileBytes = StringUtils.StringToByteArray(firstTile.Blob);
        //             images.Add(new MagickImage(firstTileBytes));
        //             while (iterator.MoveNext())
        //             {
        //                 var tile = iterator.Current;
        //                 var tileBytes = StringUtils.StringToByteArray(tile.Blob);
        //                 var tileImage = new MagickImage(tileBytes);
        //                 if (!tileImage.HasAlpha)
        //                 {
        //                     images.Clear();
        //                 }
        //                 //TODO: add upscale logic
        //                 images.Add(tileImage);
        //             }
        //         }
        //         using (var mergedImage = images.Merge()) //image magic flatten, merge, and mosaic s
        //         {
        //             var mergedImageBytes = mergedImage.ToByteArray();
        //             var blob = Convert.ToHexString(mergedImageBytes);
        //             return blob;
        //     string blob;
        //     byte[] newBytes = StringUtils.StringToByteArray(newTile.Blob);
        //     using (MagickImage newImage = new MagickImage(newBytes))
        //     {
        //         if (newImage.HasAlpha)
        //         {
        //             byte[] baseBytes = StringUtils.StringToByteArray(baseTile.Blob);
        //             using (MagickImage baseImage = new MagickImage(baseBytes))
        //             {

        //                 if (baseTile.Z != newTile.Z)
        //                 {
        //                     Upscaling.Upscale(baseImage, baseTile, newTile);
        //                 }

        //                 baseImage.Composite(newImage, CompositeOperator.SrcOver);
        //                 byte[] baseByteArr = baseImage.ToByteArray();
        //                 blob = Convert.ToHexString(baseByteArr);
        //             }
        //         }
        //         else
        //         {
        //             blob = newTile.Blob;
        //         }
        //     }

        }
    }
}
