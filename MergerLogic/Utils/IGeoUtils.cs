using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IGeoUtils
    {
        int FlipY(int z, int y);
        int FlipY(Coord coord);
        int FlipY(Tile tile);
        double DegreesPerTile(int zoom);
        Extent SnapExtentToTileGrid(Extent extent, int zoom);
        TileBounds ExtentToTileRange(Extent extent, int zoom, GridOrigin origin = GridOrigin.UPPER_LEFT);
        Extent TileRangeToExtent(TileBounds bounds);
        Extent DefaultExtent(bool isOneXOne);
    }
}
