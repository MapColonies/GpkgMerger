namespace MergerLogic.Utils
{
    public interface IUtilsFactory
    {
        T GetDataUtils<T>(string path) where T : IDataUtils;
        IFileUtils GetFileUtils(string path);
        IGpkgUtils GetGpkgUtils(string path);
        IHttpSourceUtils GetHttpUtils(string path);
        IPathPatternUtils GetPathPatternUtils(string pattern);
        IS3Utils GetS3Utils(string path);
    }
}
