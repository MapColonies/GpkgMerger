using ImageMagick;

namespace MergerLogic.Utils
{
    public class ImageUtils
    {
        public static bool IsTransparent(MagickImage image)
        {
            if(!image.HasAlpha) {
                return false;
            }

            using var pixels = image.GetPixels();
            // Check pixels to see if all are transparent
            return pixels.Select(pixel => pixel.ToColor()).Any(color => color?.A != 255);
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
