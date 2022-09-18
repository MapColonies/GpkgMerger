using MergerCli.Utils;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;

namespace MergerCli
{
    internal interface IProcess
    {
        void Start(TileFormat format, IData baseData, IData newData, int batchSize,
            BatchStatusManager batchStatusManager);

        void Validate(IData baseData, IData newData);
    }
}
