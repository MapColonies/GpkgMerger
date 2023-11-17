using MergerLogic.Clients;

namespace MergerLogic.Utils
{
    public interface IUtilsFactory
    {
        T GetDataUtils<T>(string? bucket, string path) where T : IDataUtils;
        IFileClient GetFileUtils(string path);
        IGpkgClient GetGpkgUtils(string path);
        IHttpSourceClient GetHttpUtils(string path);
        IPathPatternUtils GetPathPatternUtils(string pattern);
        IS3Client GetS3Utils(string? bucket, string path);
    }
}
