﻿using Amazon.Runtime;
using Amazon.S3;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Reflection;

namespace MergerLogic.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection RegisterMergerLogicType(this IServiceCollection collection, bool includeServiceProvider = true)
        {
            if (includeServiceProvider)
            {
                collection = collection.RegisterServiceProvider();
            }
            return collection
                .RegisterImageProcessors()
                .RegisterMergerUtils()
                .RegisterS3()
                .RegisterOpenTelemetry();
        }

        public static IServiceCollection RegisterImageProcessors(this IServiceCollection collection)
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

        public static IServiceCollection RegisterOpenTelemetry(this IServiceCollection collection)
        {
            // This is required if the collector doesn't expose an https endpoint. By default, .NET
            // only allows http2 (required for gRPC) to secure endpoints.
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            ConfigurationManager _config = new ConfigurationManager(null);

            #region Logger
            var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddOpenTelemetry(options =>
                    {
                        options.AddConsoleExporter();
                    });
                });
            collection.Replace(new ServiceDescriptor(typeof(ILoggerFactory), loggerFactory));
            #endregion Logger

            var appInfo = Assembly.GetEntryAssembly()?.GetName();
            string serviceName = appInfo?.FullName ?? "TileMerger";
            string serviceVersion = appInfo?.Version?.ToString() ?? "v0.0.0";
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(serviceName, serviceVersion: serviceVersion);

            #region Tracing
            bool tracingEnabled = _config.GetConfiguration<bool>("TRACING", "enabled");
            if (tracingEnabled)
            {
                collection.AddOpenTelemetryTracing(tracerProviderBuilder =>
                {
                    double traceRatio = _config.GetConfiguration<double>("TRACING", "ratio");
                    string traceCollectorUrl = _config.GetConfiguration("TRACING", "url");
                    tracerProviderBuilder.AddSource(serviceName)
                        .SetResourceBuilder(resourceBuilder)
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter(opt =>
                        {
                            opt.Endpoint = new Uri(traceCollectorUrl);
                            opt.ExportProcessorType =
                                ExportProcessorType.Batch; //TODO: support simple config for debug
                        })
                        .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(traceRatio)));
                });
                collection.AddSingleton(new ActivitySource(serviceName));
            }
            #endregion Tracing

            #region Metrics
            bool metricsEnabled = _config.GetConfiguration<bool>("METRICS", "enabled");
            if (metricsEnabled)
            {
                collection.AddOpenTelemetryMetrics(meterProviderBuilder =>
                {
                    string meterCollectorUrl = _config.GetConfiguration("METRICS", "url");
                    int interval = _config.GetConfiguration<int>("METRICS", "interval");
                    meterProviderBuilder.SetResourceBuilder(resourceBuilder);
                    var exporter = new OtlpMetricExporter(new OtlpExporterOptions()
                    {
                        Endpoint = new Uri(meterCollectorUrl)
                    });
                    meterProviderBuilder.AddReader(new PeriodicExportingMetricReader(exporter, interval));
                    //TODO: support console config for debug
                });
            }
            #endregion Metrics

            return collection;
        }

    }
}
