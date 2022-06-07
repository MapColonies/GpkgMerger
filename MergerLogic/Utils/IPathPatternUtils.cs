using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IPathPatternUtils
    {
        string renderUrlTemplate(Coord coords);
        string renderUrlTemplate(int x, int y, int z);
    }
}