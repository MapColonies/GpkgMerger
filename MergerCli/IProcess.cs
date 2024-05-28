using MergerCli.Utils;
using MergerLogic.DataTypes;

namespace MergerCli
{
    internal interface IProcess
    {
        void Start(IData baseData, IData newData, BatchStatusManager batchStatusManager);

        void Validate(IData baseData, IData newData, string? incompleteBatchIdentifier = null);
    }
}
