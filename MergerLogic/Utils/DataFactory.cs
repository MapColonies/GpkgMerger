﻿using Amazon.S3;
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
        private readonly string _bucket;

        public DataFactory(IConfigurationManager configuration, IPathUtils pathUtils, IServiceProvider container, ILogger<DataFactory> logger, IFileSystem fileSystem)
        {
            this._configurationManager = configuration;
            this._pathUtils = pathUtils;
            this._container = container;
            this._logger = logger;
            this._fileSystem = fileSystem;

            _bucket = this._configurationManager.GetConfiguration("S3", "bucket");
        }

        public IData CreateDataSource(string type, string path, int batchSize, bool? isOneXOne = null, GridOrigin? origin = null, Extent? extent = null, bool isBase = false)
        {
            IData data;

            switch (type.ToLower())
            {
                case "gpkg":
                    data = new Gpkg(this._configurationManager, this._container, path, batchSize, isOneXOne, origin, isBase, extent);
                    break;
                case "s3":
                    var client = this._container.GetService<IAmazonS3>();
                    if (client is null)
                    {
                        throw new Exception("s3 configuration is required");
                    }
                    path = this._pathUtils.RemoveTrailingSlash(path);
                    data = new S3(this._pathUtils, client, this._container, _bucket, path, batchSize, isOneXOne, origin);
                    break;
                case "fs":
                    data = new FS(this._pathUtils, this._container, path, batchSize, isOneXOne, origin, isBase);
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

        public IData CreateDataSource(string type, string path, int batchSize, bool isBase, Extent extent, int maxZoom, int minZoom = 0, bool? isOneXone = null, GridOrigin? origin = null)
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
                    data = new WMTS(this._container, path, batchSize, extent, isOneXone, origin, maxZoom, minZoom);
                    break;
                case "xyz":
                    data = new XYZ(this._container, path, batchSize, extent, isOneXone, origin, maxZoom, minZoom);
                    break;
                case "tms":
                    data = new TMS(this._container, path, batchSize, extent, isOneXone, origin, maxZoom, minZoom);
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
