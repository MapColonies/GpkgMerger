using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using MergerLogic.Clients;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring;
using MergerLogic.Monitoring.Metrics;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using Prometheus;
using System.Diagnostics;
using System.IO.Abstractions;
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
                .RegisterHttp() //registering http override logs configuration so it must be registered before the telemetry 
                .RegisterOpenTelemetry()
                .RegisterFileSystem()
                .RegisterPrometheusMetrics();
        }

        public static IServiceCollection RegisterImageProcessors(this IServiceCollection collection)
        {
            return collection
                .AddSingleton<ITileMerger, TileMerger>()
                .AddSingleton<ITileScaler, TileScaler>()
                .AddSingleton<IImageFormatter, ImageFormatter>();
        }

        public static IServiceCollection RegisterMergerUtils(this IServiceCollection collection)
        {
            return collection
                .AddSingleton<IConfigurationManager, ConfigurationManager>()
                .AddSingleton<IDataFactory, DataFactory>()
                .AddSingleton<IOneXOneConvertor, OneXOneConvertor>()
                .AddSingleton<IPathUtils, PathUtils>()
                .AddSingleton<ITimeUtils, TimeUtils>()
                .AddSingleton<IUtilsFactory, UtilsFactory>()
                .AddSingleton<IGeoUtils, GeoUtils>();
        }

        public static IServiceCollection RegisterServiceProvider(this IServiceCollection collection)
        {
            return collection.AddSingleton<IServiceProvider>(sp => sp); ;
        }

        public static IServiceCollection RegisterFileSystem(this IServiceCollection collection)

        {
            return collection.AddSingleton<IFileSystem, FileSystem>();
        }

        public static IServiceCollection RegisterS3(this IServiceCollection collection)
        {
            return collection.AddSingleton<IAmazonS3>(sp =>
            {
                var config = sp.GetRequiredService<IConfigurationManager>();

                // Get configuration if should log S3 SDK operations to console
                bool logToConsole = config.GetConfiguration<bool>("S3", "logToConsole");
                if (logToConsole)
                {
                    AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;
                }

                string s3Url = config.GetConfiguration("S3", "url");
                string? accessKey = Environment.GetEnvironmentVariable(EnvironmentVariablesAWSCredentials.ENVIRONMENT_VARIABLE_ACCESSKEY);
                string? secretKey = Environment.GetEnvironmentVariable(EnvironmentVariablesAWSCredentials.ENVIRONMENT_VARIABLE_SECRETKEY);
                int timeoutSec = config.GetConfiguration<int>("S3", "request", "timeoutSec");
                int retries = config.GetConfiguration<int>("S3", "request", "retries");

                if (string.IsNullOrEmpty(s3Url) || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
                {
                    throw new Exception("s3 configuration is required");
                }
                if (retries < 1)
                {
                    throw new Exception("s3 request retries should have a value of at least 1");
                }

                var s3Config = new AmazonS3Config
                {
                    ServiceURL = s3Url,
                    ForcePathStyle = true,
                    Timeout = new TimeSpan(0, 0, timeoutSec),
                    MaxErrorRetry = retries,
                };

                var credentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonS3Client(credentials, s3Config);
            });
        }

        public static IServiceCollection RegisterHttp(this IServiceCollection collection)
        {
            ConfigurationManager _config = new ConfigurationManager(null);
            var maxAttempts = _config.GetConfiguration<int>("HTTP", "retries");
            var retryPolicy = HttpPolicyExtensions.HandleTransientHttpError()
                .WaitAndRetryAsync(maxAttempts, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            collection.AddHttpClient("httpClient")
                .AddPolicyHandler(retryPolicy);
            collection.AddHttpClient("httpClientWithoutRetry")
                .AddPolicyHandler(retryPolicy);
            collection.AddTransient<HttpClient>(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("httpClient"));
            collection.AddTransient<IHttpRequestUtils, HttpRequestUtils>();
            return collection;
        }

        public static IServiceCollection RegisterOpenTelemetry(this IServiceCollection collection)
        {
            // This is required if the collector doesn't expose an https endpoint. By default, .NET
            // only allows http2 (required for gRPC) to secure endpoints.
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            ConfigurationManager _config = new ConfigurationManager(null);
            var appInfo = Assembly.GetEntryAssembly()?.GetName();
            string serviceName = appInfo?.Name ?? "TileMerger";
            string serviceVersion = appInfo?.Version?.ToString() ?? "v0.0.0";
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(serviceName, serviceVersion: serviceVersion);

            #region Logger
            collection.AddLogging(configure =>
            {
                configure.ClearProviders();
                configure.AddOpenTelemetry(options =>
                    {
                        //console exporter has hardcoded multiline log format so it cant be used
                        //options.AddConsoleExporter();
                        //disable lgtm not disposed alert as it is global singleton that is used for the entire app life
                        options.SetResourceBuilder(resourceBuilder);
                        options.AddProcessor(
                            new SimpleLogRecordExportProcessor(new OpenTelemetryFormattedConsoleExporter(new ConsoleExporterOptions()))); //lgtm [cs/local-not-disposed]
                    });
            });
            #endregion Logger

            #region Tracing
            bool tracingEnabled = _config.GetConfiguration<bool>("TRACING", "enabled");
            if (tracingEnabled)
            {
                collection.AddOpenTelemetry().WithTracing(tracerProviderBuilder =>
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
            }
            //disable lgtm not disposed alert as it is global singleton that is used for the entire app life
            collection.AddSingleton(new ActivitySource(serviceName)); //lgtm [cs/local-not-disposed]
            #endregion Tracing

            return collection;
        }

        public static IServiceCollection RegisterPrometheusMetrics(this IServiceCollection collection)
        {
            ConfigurationManager _config = new ConfigurationManager(null);
            bool metricsEnabled = _config.GetConfiguration<bool>("METRICS", "enabled");
            collection.AddSingleton<IMetricsProvider, MetricsProvider>();
            if (metricsEnabled)
            {
                int metricsPort = _config.GetConfiguration<int>("METRICS", "port");
                MetricServer metricsServer = new MetricServer(port: metricsPort);
                metricsServer.Start();
            }

            return collection;
        }


    }
}
