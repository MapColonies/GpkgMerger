using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

namespace MergerLogic.Utils
{
    public class UtilsFactory : IUtilsFactory
    {
        private readonly IPathUtils _pathUtils;
        private readonly ITimeUtils _timeUtils;
        private readonly IGeoUtils _geoUtils;
        private readonly IFileSystem _fileSystem;
        private readonly IServiceProvider _container;
        private IHttpRequestUtils _httpRequestUtils;

        public UtilsFactory(IPathUtils pathUtils, ITimeUtils timeUtils, IGeoUtils geoUtils, IFileSystem fileSystem, IServiceProvider container, IHttpRequestUtils httpRequestUtils)
        {
            this._pathUtils = pathUtils;
            this._timeUtils = timeUtils;
            this._geoUtils = geoUtils;
            this._fileSystem = fileSystem;
            this._container = container;
            this._httpRequestUtils = httpRequestUtils;
        }

        #region dataUtils

        public IFileUtils GetFileUtils(string path)
        {
            return new FileUtils(path, this._pathUtils, this._geoUtils, this._fileSystem);
        }

        public IGpkgUtils GetGpkgUtils(string path)
        {
            var logger = this._container.GetRequiredService<ILogger<GpkgUtils>>();
            return new GpkgUtils(path, this._timeUtils, logger, this._fileSystem, this._geoUtils);
        }

        public IHttpSourceUtils GetHttpUtils(string path)
        {
            IPathPatternUtils pathPatternUtils = this.GetPathPatternUtils(path);
            return new HttpSourceUtils(path, this._httpRequestUtils, pathPatternUtils, this._geoUtils);
        }

        public IS3Utils GetS3Utils(string path)
        {
            string bucket = this._container.GetRequiredService<IConfigurationManager>().GetConfiguration("S3", "bucket");
            IAmazonS3? client = this._container.GetService<IAmazonS3>();
            if (client is null || bucket == string.Empty)
            {
                throw new Exception("S3 Data utils requires s3 client to be configured");
            }

            return new S3Utils(client, this._pathUtils, this._geoUtils, bucket, path);
        }

        public T GetDataUtils<T>(string path) where T : IDataUtils
        {
            if (typeof(IFileUtils).IsAssignableFrom(typeof(T)))
            {
                return (T)(Object)this.GetFileUtils(path);
            }
            if (typeof(IGpkgUtils).IsAssignableFrom(typeof(T)))
            {
                return (T)(Object)this.GetGpkgUtils(path);
            }
            if (typeof(IHttpSourceUtils).IsAssignableFrom(typeof(T)))
            {
                return (T)(Object)this.GetHttpUtils(path);
            }
            if (typeof(IS3Utils).IsAssignableFrom(typeof(T)))
            {
                return (T)(Object)this.GetS3Utils(path);
            }
            throw new NotImplementedException("Invalid Utils type");
        }

        #endregion dataUtils

        public IPathPatternUtils GetPathPatternUtils(string pattern)
        {
            return new PathPatternUtils(pattern);
        }
    }
}
