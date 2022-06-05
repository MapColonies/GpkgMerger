using MergerLogic.Batching;

namespace MergerLogic.Utils
{
    public class FileUtils : DataUtils
    {
        public FileUtils(string path) : base(path) { }
        private static string GetFileString(string path)
        {
            if (File.Exists(path))
            {
                byte[] fileBytes = File.ReadAllBytes(path);
                return StringUtils.ByteArrayToString(fileBytes);
            }
            else
            {
                return null;
            }
        }

        public override Tile GetTile(int z, int x, int y)
        {
            // Convert to TMS
            y = GeoUtils.FlipY(z, y);
            string tilePath = PathUtils.GetTilePath(this.path, z, x, y);
            string blob = GetFileString(tilePath);
            if (blob == null)
            {
                return null;
            }
            // Convert from TMS
            y = GeoUtils.FlipY(z, y);
            return new Tile(z, x, y, blob, blob.Length);
        }

        public override bool TileExists(int z, int x, int y)
        {
            string fullPath = PathUtils.GetTilePath(this.path, z, x, y);
            return File.Exists(fullPath);
        }
    }
}
