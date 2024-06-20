using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProcessing
{
    public interface ITileMerger
    {
        Tile? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords, TileFormatStrategy strategy, bool uploadOnly = false);
    }
}
