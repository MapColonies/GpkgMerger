using MergerLogic.Batching;

namespace MergerLogic.Utils
{
    public class FileUtils : DataUtils
    {
        public FileUtils(string path) : base(path) { }

        public override Tile GetTile(int z, int x, int y)
        {
            // Convert to TMS
            y = GeoUtils.FlipY(z, y);
            string tilePath = PathUtils.GetTilePath(this.path, z, x, y);
            if (File.Exists(tilePath))
            {
                byte[] fileBytes = File.ReadAllBytes(tilePath);
                y = GeoUtils.FlipY(z, y);
                return new Tile(z, x, y, fileBytes);
            }
            else
            {
                return null;
            }
        }

        //private bool Exists(string path)
        //{
            
        //}
    }
}
