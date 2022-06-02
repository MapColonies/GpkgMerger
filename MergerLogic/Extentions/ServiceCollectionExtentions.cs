

using MergerLogic.ImageProccessing;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace MergerLogic.Extentions
{
    public static class ServiceCollectionExtentions
    {
        public static void RegisterMergerLogicType(this IServiceCollection collection)
        {
            collection.RegisterImageProccessors();
            collection.RegisterMergerUtils();
        }

        public static void RegisterImageProccessors(this IServiceCollection collection)
        {
            collection.AddSingleton<ITileMerger, TileMerger>();
            collection.AddSingleton<ITileScaler, TileScaler>();
        }

        public static void RegisterMergerUtils(this IServiceCollection collection)
        {
            collection.AddSingleton<IConfigurationManager, ConfigurationManager>();
            collection.
        }
    }
}
