

using MergerLogic.ImageProccessing;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace MergerLogic.Extentions
{
    public static class ServiceCollectionExtentions
    {
        public static IServiceCollection RegisterMergerLogicType(this IServiceCollection collection)
        {
            return collection
                .RegisterImageProccessors()
                .RegisterMergerUtils();
        }

        public static IServiceCollection RegisterImageProccessors(this IServiceCollection collection)
        {
            return collection
                .AddSingleton<ITileMerger, TileMerger>()
                .AddSingleton<ITileScaler, TileScaler>();
        }

        public static IServiceCollection RegisterMergerUtils(this IServiceCollection collection)
        {
            return collection
                .AddSingleton<IConfigurationManager, ConfigurationManager>()
                .AddSingleton<IDataFactory,DataFactory>();
        }
    }
}
