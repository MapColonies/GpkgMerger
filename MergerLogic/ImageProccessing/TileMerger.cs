using ImageMagick;
using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProccessing
{
    public class TileMerger : ITileMerger
    {

        public byte[]? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords)
        {
            Tile lastProccessedTile;
            var images = this.getImageList(tiles, targetCoords, out lastProccessedTile);
            switch (images.Count)
            {
                case 0:
                    return null;
                case 1:
                    images[0].Dispose();
                    return lastProccessedTile?.GetImageBytes();
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
                            return mergedImageBytes;
                        }
                    }
            }
        }

        private List<MagickImage> getImageList(List<CorrespondingTileBuilder> tiles, Coord targetCoords, out Tile lastProccessedTile)
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
                    var tileBytes = tile.GetImageBytes();
                    tileImage = new MagickImage(tileBytes);
                    if (tile.Z < targetCoords.z)
                    {
                        TileScaler.Upscale(tileImage, tile, targetCoords);
                    }
                    images.Add(tileImage);
                    if (!tileImage.HasAlpha)
                    {
                        break;
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
            }
            return images;
        }
    }
}
