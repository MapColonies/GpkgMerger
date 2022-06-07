using MergerLogic.DataTypes;

namespace MergerCli
{
    internal interface ISourceParser
    {
        List<Data> ParseSources(string[] args, int batchSize);
    }
}