using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.ImageProccessing
{
    public interface ITileMerger
    {
        byte[]? MergeTiles(List<CorrespondingTileBuilder> tiles, Coord targetCoords);
    }
}
