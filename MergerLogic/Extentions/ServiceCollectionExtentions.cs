using Amazon.Runtime;
using Amazon.S3;
using MergerLogic.ImageProccessing;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace MergerLogic.Extentions
{
    public static class ServiceCollectionExtentions
    {
        public static IServiceCollection RegisterMergerLogicType(this IServiceCollection collection, bool includeServiceProvider = true)
        {
            if (includeServiceProvider)
            {
                collection = collection.RegisterServiceProvider();
            }
            return collection
                .RegisterImageProccessors()
                .RegisterMergerUtils()
                .RegisterS3();
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
                .AddSingleton<IDataFactory, DataFactory>()
                .AddSingleton<IOneXOneConvetor, OneXOneConvetor>()
                .AddSingleton<IPathUtils, PathUtils>()
                .AddSingleton<IStringUtils, StringUtils>()
                .AddSingleton<ITimeUtils, TimeUtils>()
                .AddSingleton<IUtilsFactory, UtilsFactory>();
        }

        public static IServiceCollection RegisterServiceProvider(this IServiceCollection collection)
        {
            return collection.AddSingleton<IServiceProvider>(sp => sp); ;
        }

        public static IServiceCollection RegisterS3(this IServiceCollection collection)
        {
            return collection.AddSingleton<IAmazonS3>(sp =>
            {
                var config = sp.GetRequiredService<IConfigurationManager>();
                string s3Url = config.GetConfiguration("S3", "url");
                string accessKey = Environment.GetEnvironmentVariable("S3_ACCESS_KEY");
                string secretKey = Environment.GetEnvironmentVariable("S3_SECRET_KEY");

                if (string.IsNullOrEmpty(s3Url) || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
                {
                    throw new Exception("s3 configuration is required");
                }

                var s3Config = new AmazonS3Config
                {
                    RegionEndpoint = Amazon.RegionEndpoint.USEast1,
                    ServiceURL = s3Url,
                    ForcePathStyle = true
                };
                var credentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonS3Client(credentials, s3Config);

            });
        }

    }
}
