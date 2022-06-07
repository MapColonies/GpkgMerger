namespace MergerLogic.Utils
{
    public interface IUtilsFactory
    {
        T GetDataUtils<T>(string path) where T : DataUtils;
        FileUtils GetFileUtiles(string path);
        GpkgUtils GetGpkgUtils(string path);
        httpUtils GetHttpUtils(string path);
        IPathPatternUtils GetPathPatternUtils(string pattern);
        S3Utils GetS3Utils(string path);
    }
}