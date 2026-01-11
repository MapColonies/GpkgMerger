using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;

namespace MergerLogic.Utils
{
    public interface IOneXOneConvertor
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
