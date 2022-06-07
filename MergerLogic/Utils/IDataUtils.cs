using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IDataUtils
    {
        Tile GetTile(Coord coord);
        Tile GetTile(int z, int x, int y);
        bool TileExists(int z, int x, int y);
    }
}