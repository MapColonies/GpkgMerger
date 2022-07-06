using ImageMagick;
using MergerLogic.Batching;

namespace MergerLogic.ImageProcessing
{
    public class ImageFormatter : IImageFormatter
    {
        public Tile ToPng(Tile tile)
        {
            var image = this.ToPng(tile.GetImageBytes());
            return new Tile(tile.Z, tile.X, tile.Y, image);
        }
        public byte[] ToPng(byte[] imageData)
        {
            using (var image = new MagickImage(imageData))
            {
                this.ToPng(image);
                return image.ToByteArray();
            }
        }

        public void ToPng(IMagickImage image)
        {
            image.Format = MagickFormat.Png;
        }
    }
}
