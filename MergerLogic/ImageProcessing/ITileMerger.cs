using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public interface ITileMerger
    {
        byte[]? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords, TileFormat format);
    }
}
