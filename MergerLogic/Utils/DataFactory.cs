using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public class DataFactory : IDataFactory
    {
        private IConfigurationManager _configurationManager;
        public DataFactory(IConfigurationManager configuration)
        {
            this._configurationManager = configuration;
        }

        public Data CreateDatasource(string type, string path, int batchSize, bool isOneXOne, TileGridOrigin? origin = null, bool isBase = false)
        {
            Data data;
            switch (type.ToLower())
            {
                case "gpkg":
                    if (origin == null)
                        data = new Gpkg(this._configurationManager,path, batchSize, isOneXOne);
                    else
                        data = new Gpkg(this._configurationManager, path, batchSize, isOneXOne, origin.Value);
                    break;
                case "s3":
                    string s3Url = this._configurationManager.GetConfiguration("S3", "url");
                    string bucket = this._configurationManager.GetConfiguration("S3", "bucket");
                    var client = S3.GetClient(s3Url);
                    path = PathUtils.RemoveTrailingSlash(path);
                    if (origin == null)
                        data = new S3(client, bucket, path, batchSize, isOneXOne);
                    else
                        data = new S3(client, bucket, path, batchSize, isOneXOne, origin.Value);
                    break;
                case "fs":
                    if (origin == null)
                        data = new FS(DataType.FOLDER, path, batchSize, isOneXOne, isBase);
                    else
                        data = new FS(DataType.FOLDER, path, batchSize, isOneXOne, isBase, origin.Value);
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
                    Console.WriteLine($"base data at path '{path}' does not exists and will be created");
                else
                    throw new Exception($"path '{path}' to data does not exist.");
            }

            return data;
        }

        public Data CreateDatasource(string type, string path, int batchSize, bool isBase, Extent extent, int maxZoom, int minZoom = 0, bool isOneXone = false, TileGridOrigin? origin = null)
        {
            Data data;
            type = type.ToLower();
            switch (type)
            {
                case "gpkg":
                case "s3":
                case "fs":
                    return this.CreateDatasource(type, path, batchSize, isOneXone, origin, isBase);
            };
            if (isBase)
            {
                throw new Exception("web tile source cannot be used as base (target) layer");
            }
            switch (type)
            {
                case "wmts":
                    if (origin == null)
                        data = new WMTS(DataType.WMTS, path, batchSize, extent, maxZoom, minZoom, isOneXone);
                    else
                        data = new WMTS(DataType.WMTS, path, batchSize, extent, maxZoom, minZoom, isOneXone, origin.Value);
                    break;
                case "xyz":
                    if (origin == null)
                        data = new XYZ(DataType.XYZ, path, batchSize, extent, maxZoom, minZoom, isOneXone);
                    else
                        data = new XYZ(DataType.XYZ, path, batchSize, extent, maxZoom, minZoom, isOneXone, origin.Value);
                    break;
                case "tms":
                    if (origin == null)
                        data = new TMS(DataType.TMS, path, batchSize, extent, maxZoom, minZoom, isOneXone);
                    else
                        data = new TMS(DataType.TMS, path, batchSize, extent, maxZoom, minZoom, isOneXone, origin.Value);
                    break;
                default:
                    throw new Exception($"Currently there is no support for the data type '{type}'");
            }

            if (!data.Exists())
            {
                //skip existence validation for base data to allow creation of new data for FS and S3
                if (isBase)
                    Console.WriteLine($"base data at path '{path}' does not exists and will be created");
                else
                    throw new Exception($"path '{path}' to data does not exist.");
            }

            return data;
        }
    }
}
