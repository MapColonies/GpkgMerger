using System.IO;
using GpkgMerger.Src.Batching;

namespace GpkgMerger.Src.Utils
{
    public class FileUtils : DataUtils
    {
        public FileUtils(string path) : base(path) { }
        private static string GetFileString(string path)
        {
            byte[] fileBytes = File.ReadAllBytes(path);
            return StringUtils.ByteArrayToString(fileBytes);
        }

        public override Tile GetTile(int z, int x, int y)
        {
            string tilePath = PathUtils.GetTilePath(this.path, z, x, y);
            string blob = GetFileString(tilePath);
            return new Tile(z, x, y, blob, blob.Length);
        }
    }
}
