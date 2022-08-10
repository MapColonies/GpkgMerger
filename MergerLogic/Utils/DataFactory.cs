using Amazon.S3;
using MergerLogic.Batching;
using MergerLogic.DataTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

namespace MergerLogic.Utils
{
    public class DataFactory : IDataFactory
    {
        private readonly IConfigurationManager _configurationManager;
        private readonly IPathUtils _pathUtils;
        private readonly IServiceProvider _container;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;
        private readonly string _outputPath;
        private readonly string _bucket;

        public DataFactory(IConfigurationManager configuration, IPathUtils pathUtils, IServiceProvider container, ILogger<DataFactory> logger, IFileSystem fileSystem)
        {
            this._configurationManager = configuration;
            this._pathUtils = pathUtils;
            this._container = container;
            this._logger = logger;
            this._fileSystem = fileSystem;

            _outputPath = this._configurationManager.GetConfiguration("GENERAL", "outputPath");
            _bucket = this._configurationManager.GetConfiguration("S3", "bucket");
        }

        public IData CreateDataSource(string type, string path, int batchSize, bool isOneXOne, GridOrigin? origin = null, Extent? extent = null, bool isBase = false)
        {
            IData data;

            switch (type.ToLower())
            {
                case "gpkg":
                    path = this._fileSystem.Path.Join(_outputPath, path);
                    if (origin == null)
                        data = new Gpkg(this._configurationManager, this._container, path, batchSize, isBase, isOneXOne, extent);
                    else
                        data = new Gpkg(this._configurationManager, this._container, path, batchSize, isBase, isOneXOne, extent, origin.Value);
                    break;
                case "s3":
                    var client = this._container.GetService<IAmazonS3>();
                    if (client is null)
                    {
                        throw new Exception("s3 configuration is required");
                    }
                    path = this._pathUtils.RemoveTrailingSlash(path);
                    if (origin == null)
                        data = new S3(this._pathUtils, client, this._container, _bucket, path, batchSize, isOneXOne);
                    else
                        data = new S3(this._pathUtils, client, this._container, _bucket, path, batchSize, isOneXOne, origin.Value);
                    break;
                case "fs":
                    path = this._fileSystem.Path.Join(_outputPath, path);
                    if (origin == null)
                        data = new FS(this._pathUtils, this._container, path, batchSize, isOneXOne, isBase);
                    else
                        data = new FS(this._pathUtils, this._container, path, batchSize, isOneXOne, isBase, origin.Value);
                    break;
                case "wmts":
                case "xyz":
                case "tms":
                    throw new Exception("web tile source requires extent, and zoom restrictions");
                default:
                    throw new Exception($"Currently there is no support for the data type '{type}'");
            }

            if (!data.Exists())
            {
                //skip existence validation for base data to allow creation of new data for FS and S3
                if (isBase)
                    this._logger.LogInformation($"base data at path '{path}' does not exists and will be created");
                else
                    throw new Exception($"path '{path}' to data does not exist.");
            }

            return data;
        }

        public IData CreateDataSource(string type, string path, int batchSize, bool isBase, Extent extent, int maxZoom, int minZoom = 0, bool isOneXone = false, GridOrigin? origin = null)
        {
            IData data;
            type = type.ToLower();
            switch (type)
            {
                case "gpkg":
                case "s3":
                case "fs":
                    return this.CreateDataSource(type, path, batchSize, isOneXone, origin, extent, isBase);
            };
            if (isBase)
            {
                throw new Exception("web tile source cannot be used as base (target) layer");
            }
            switch (type)
            {
                case "wmts":
                    if (origin == null)
                        data = new WMTS(this._container, path, batchSize, extent, maxZoom, minZoom, isOneXone);
                    else
                        data = new WMTS(this._container, path, batchSize, extent, maxZoom, minZoom, isOneXone, origin.Value);
                    break;
                case "xyz":
                    if (origin == null)
                        data = new XYZ(this._container, path, batchSize, extent, maxZoom, minZoom, isOneXone);
                    else
                        data = new XYZ(this._container, path, batchSize, extent, maxZoom, minZoom, isOneXone, origin.Value);
                    break;
                case "tms":
                    if (origin == null)
                        data = new TMS(this._container, path, batchSize, extent, maxZoom, minZoom, isOneXone);
                    else
                        data = new TMS(this._container, path, batchSize, extent, maxZoom, minZoom, isOneXone, origin.Value);
                    break;
                default:
                    throw new Exception($"Currently there is no support for the data type '{type}'");
            }

            if (!data.Exists())
            {
                //skip existence validation for base data to allow creation of new data for FS and S3
                if (isBase)
                    this._logger.LogInformation($"base data at path '{path}' does not exists and will be created");
                else
                    throw new Exception($"path '{path}' to data does not exist.");
            }

            return data;
        }
    }
}
