using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MergerLogic.Utils
{
    public class UtilsFactory : IUtilsFactory
    {
        private IServiceProvider _container;

        public UtilsFactory(IServiceProvider container)
        {
            this._container = container;
        }

        #region dataUtils

        public FileUtils GetFileUtiles(string path)
        {
            return new FileUtils(path);
        }

        public GpkgUtils GetGpkgUtils(string path)
        {
            return new GpkgUtils(path);
        }

        public httpUtils GetHttpUtils(string path)
        {
            IPathPatternUtils pathPatternUtils = this.GetPathPatternUtils(path);
            return new httpUtils(path, pathPatternUtils);
        }

        public S3Utils GetS3Utils(string path)
        {
            string bucket = this._container.GetRequiredService<IConfigurationManager>().GetConfiguration("S3", "bucket");
            IAmazonS3? client = this._container.GetService<IAmazonS3>();
            if (client is null || bucket == string.Empty)
            {
                throw new Exception("S3 Data utills requires s3 client to be configured");
            }

            return new S3Utils(client, path, bucket);
        }

        public T GetDataUtils<T>(string path) where T : DataUtils
        {
            //TODO: replace with interfaces
            if (typeof(FileUtils).IsAssignableFrom(typeof(T)))
            {
                return (T)(Object)this.GetFileUtiles(path);
            }
            if (typeof(GpkgUtils).IsAssignableFrom(typeof(T)))
            {
                return (T)(Object)this.GetGpkgUtils(path);
            }
            if (typeof(httpUtils).IsAssignableFrom(typeof(T)))
            {
                return (T)(Object)this.GetHttpUtils(path);
            }
            if (typeof(S3Utils).IsAssignableFrom(typeof(T)))
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
