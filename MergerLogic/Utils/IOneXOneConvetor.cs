using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IOneXOneConvetor
    {
        Coord FromTwoXOne(Coord twoXOneCoords);
        Coord FromTwoXOne(int z, int x, int y);
        Tile FromTwoXOne(Tile tile);
        Coord ToTwoXOne(Coord oneXOneCoords);
        Coord ToTwoXOne(int z, int x, int y);
        Tile ToTwoXOne(Tile tile);
        Coord? TryFromTwoXOne(Coord cords);
        Coord? TryFromTwoXOne(int z, int x, int y);
        Tile? TryFromTwoXOne(Tile tile);
        Coord? TryToTwoXOne(int z, int x, int y);
        Tile? TryToTwoXOne(Tile tile);
    }
}
