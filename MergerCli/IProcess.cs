using MergerCli.Utils;
using MergerLogic.DataTypes;

namespace MergerCli
{
    internal interface IProcess
    {
        void Start(IData baseData, IData newData, int batchSize, BatchStatusManager batchStatusManager);
        void Validate(IData baseData, IData newData);
    }
}
