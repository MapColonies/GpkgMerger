using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;

namespace MergerLogic.Utils
{
    public interface IDataUtils
    {
        bool IsValidGrid(bool isOneXOne = false);
        Tile? GetTile(Coord coord, TileFormat? format);
        Tile? GetTile(int z, int x, int y, TileFormat? format);
        bool TileExists(int z, int x, int y, TileFormat? format);
    }
}
