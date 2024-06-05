using ImageMagick;

namespace MergerLogic.Utils
{
    public class ImageUtils
    {
        public static bool IsTransparent(MagickImage image)
        {
            if (!image.HasAlpha)
            {
                return false;
            }

            using var pixels = image.GetPixels();
            
            foreach (var pixel in pixels)
            {
                if (pixel.ToColor()?.A != 255) {
                    return true;
                }
            }
            
            return false;
        }

        public static bool IsFullyTransparent(MagickImage image)
        {
            if (!image.HasAlpha)
            {
                return false;
            }

            using var pixels = image.GetPixels();
            // Check pixels to see if all are fully transparent
            return pixels.Select(pixel => pixel.ToColor()).All(color => color?.A == 0);
        }

        // public static bool IsEmpty(MagickImage image)
        // {
        //     using var pixels = image.GetPixels();
        //     // Check pixels to see if all are white or black
        //     // From: https://stackoverflow.com/questions/3064854/determine-if-alpha-channel-is-used-in-an-image
        //     var colors = pixels.Select(pixel => pixel.ToColor()).ToList();
        //     return colors.All(color => (MagickColor)color == MagickColors.White) || 
        //            colors.All(color => (MagickColor)color == MagickColors.Black);
        // }
    }
}
