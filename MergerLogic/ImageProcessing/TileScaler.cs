using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public class TileScaler : ITileScaler
    {
        private const int TILE_WIDTH = 256;
        private const int TILE_HEIGHT = 256;

        public void Upscale(MagickImage baseImage, Tile baseTile, Coord targetCoords)
        {
            int zoomLevelDiff = targetCoords.Z - baseTile.Z;
            int scale = 1 << zoomLevelDiff;

            double tilePartX = targetCoords.X % scale;
            double tilePartY = targetCoords.Y % scale;
            double tileSize = TILE_HEIGHT / (double)scale;

            int pixleX = (int)(tilePartX * tileSize);
            int pixleY = (int)(tilePartY * tileSize);
            int srcSize = Math.Max((int)tileSize, 1);

            var scaledImage = new MagickImage(MagickColor.FromRgba(0,0,0,0),TILE_WIDTH,TILE_HEIGHT);
            int maxSrcX = pixleX + srcSize;
            int maxSrcY = pixleY + srcSize;
            var srcPixels = baseImage.GetPixels();
            var targetPixels = scaledImage.GetPixels();
            for (int i = pixleX; i < maxSrcX; i++)
            {
                for (int j = pixleY; j < maxSrcY; j++)
                {
                    var srcPixel = srcPixels.GetPixel(i, j);
                            targetPixels.SetArea(it);
                    //int maxChunkX = (i + 1) * scale;
                    //for (int x = i * scale; x < maxChunkX; x++)
                    //{
                    //    int maxChunkY = (j + 1) * scale;
                    //    for (int y = j * scale; y < maxChunkY; y++)
                    //    {
                    //    }
                    //}
                }
            }

            MagickGeometry geometry = new MagickGeometry(pixleX, pixleY, imageWidth, imageHeight);
            baseImage.Crop(geometry);
            baseImage.RePage();
            //baseImage.Resize(TILE_WIDTH, TILE_HEIGHT);
            baseImage.Scale(new Percentage(scale * 100));
        }
    }
}
