using MergerCli.Utils;
using MergerLogic.DataTypes;

namespace MergerCli
{
    internal interface IProccess
    {
        void Start(Data baseData, Data newData, int batchSize, BatchStatusManager batchStatusManager);
        void Validate(Data baseData, Data newData);
    }
}