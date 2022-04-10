namespace GpkgMerger.Src.Utils
{
    public static class PathUtils
    {
        public static string RemoveTrailingSlash(string path)
        {
            // Remove / from the end of the path if exists
            if (path.EndsWith('/'))
            {
                path = path.Remove(path.Length - 1);
            }
            return path;
        }
    }
}
