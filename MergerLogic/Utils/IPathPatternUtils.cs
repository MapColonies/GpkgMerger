using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public interface IPathPatternUtils
    {
        string RenderUrlTemplate(Coord coords);
        string RenderUrlTemplate(int x, int y, int z);
    }
}
