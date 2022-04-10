namespace GpkgMerger.Src.Utils
{
    public static class PathUtils
    {
        public static string RemoveTrailingSlash(string path)
        {
            return path.TrimEnd('/');
        }
    }
}
