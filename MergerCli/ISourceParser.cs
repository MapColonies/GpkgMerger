using MergerLogic.DataTypes;

namespace MergerCli
{
    internal interface ISourceParser
    {
        List<IData> ParseSources(string[] args, int batchSize);
    }
}
