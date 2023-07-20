using MergerLogic.Batching;
using MergerLogic.DataTypes;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MergerLogic.Utils
{
    public class DataFactory : IDataFactory
    {
        private readonly IConfigurationManager _configurationManager;
        private readonly IPathUtils _pathUtils;
        private readonly IServiceProvider _container;
        private readonly ILogger _logger;

        public DataFactory(IConfigurationManager configuration, IPathUtils pathUtils, IServiceProvider container, ILogger<DataFactory> logger)
        {
            this._configurationManager = configuration;
            this._pathUtils = pathUtils;
            this._container = container;
            this._logger = logger;
        }

        public IData CreateDataSource(string type, string path, int batchSize, Grid? grid = null, GridOrigin? origin = null, Extent? extent = null, bool isBase = false)
        {
            IData data;

            switch (type.ToLower())
            {
                case "gpkg":
                    data = new Gpkg(this._configurationManager, this._container, path, batchSize, grid, origin, isBase, extent);
                    break;
                case "s3":
                    path = this._pathUtils.RemoveTrailingSlash(path);
                    data = new S3(this._pathUtils, this._container, path, batchSize, grid, origin, isBase);
                    break;
                case "fs":
                    data = new FS(this._pathUtils, this._container, path, batchSize, grid, origin, isBase);
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
                    this._logger.LogInformation($"{MethodBase.GetCurrentMethod().Name} base data at path '{path}' does not exists and will be created");
                else
                    throw new Exception($"path '{path}' to data does not exist.");
            }

            return data;
        }

        public IData CreateDataSource(string type, string path, int batchSize, bool isBase, Extent extent, int maxZoom, int minZoom = 0, Grid? grid = null, GridOrigin? origin = null)
        {
            IData data;
            type = type.ToLower();
            switch (type)
            {
                case "gpkg":
                case "s3":
                case "fs":
                    return this.CreateDataSource(type, path, batchSize, grid, origin, extent, isBase);
            };
            if (isBase)
            {
                throw new Exception("web tile source cannot be used as base (target) layer");
            }
            switch (type)
            {
                case "wmts":
                    data = new WMTS(this._container, path, batchSize, extent, grid, origin, maxZoom, minZoom);
                    break;
                case "xyz":
                    data = new XYZ(this._container, path, batchSize, extent, grid, origin, maxZoom, minZoom);
                    break;
                case "tms":
                    data = new TMS(this._container, path, batchSize, extent, grid, origin, maxZoom, minZoom);
                    break;
                default:
                    throw new Exception($"Currently there is no support for the data type '{type}'");
            }

            if (!data.Exists())
            {
                //skip existence validation for base data to allow creation of new data for FS and S3
                if (isBase)
                    this._logger.LogInformation($"{MethodBase.GetCurrentMethod().Name} base data at path '{path}' does not exists and will be created");
                else
                    throw new Exception($"path '{path}' to data does not exist.");
            }

            return data;
        }
    }
}
