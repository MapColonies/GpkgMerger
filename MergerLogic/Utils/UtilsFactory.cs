using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;

namespace MergerLogic.Utils
{
    public class UtilsFactory : IUtilsFactory
    {
        private IPathUtils _pathUtils;
        private ITimeUtils _timeUtils;
        private IServiceProvider _container;
        private IHttpRequestUtils _httpRequestUtils;

        public UtilsFactory(IPathUtils pathUtils, ITimeUtils timeUtils, IServiceProvider container, IHttpRequestUtils httpRequestUtils)
        {
            this._pathUtils = pathUtils;
            this._timeUtils = timeUtils;
            this._container = container;
            this._httpRequestUtils = httpRequestUtils;
        }

        #region dataUtils

        public IFileUtils GetFileUtils(string path)
        {
            return new FileUtils(path, this._pathUtils);
        }

        public IGpkgUtils GetGpkgUtils(string path)
        {
            return new GpkgUtils(path, this._timeUtils);
        }

        public IHttpSourceUtils GetHttpUtils(string path)
        {
            IPathPatternUtils pathPatternUtils = this.GetPathPatternUtils(path);
            return new HttpSourceUtils(this._httpRequestUtils, path, pathPatternUtils);
        }

        public IS3Utils GetS3Utils(string path)
        {
            string bucket = this._container.GetRequiredService<IConfigurationManager>().GetConfiguration("S3", "bucket");
            IAmazonS3? client = this._container.GetService<IAmazonS3>();
            if (client is null || bucket == string.Empty)
            {
                throw new Exception("S3 Data utills requires s3 client to be configured");
            }

            return new S3Utils(client, this._pathUtils, bucket, path);
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
