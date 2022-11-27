using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;

namespace MergerCli
{
    internal interface ISourceParser
    {
        List<IData> ParseSources(string[] args, int batchSize, out TileFormat format);
    }
}
