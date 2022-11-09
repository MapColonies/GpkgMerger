using MergerCli.Utils;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;

namespace MergerCli
{
    internal interface IProcess
    {
        long Start(TileFormat targetFormat, IData baseData, IData newData, int batchSize,
            BatchStatusManager batchStatusManager);

        void Validate(IData baseData, IData newData);
    }
}
