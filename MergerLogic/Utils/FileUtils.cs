using MergerLogic.Batching;

namespace MergerLogic.Utils
{
    public class FileUtils : DataUtils
    {
        public FileUtils(string path) : base(path) { }

        public override Tile GetTile(int z, int x, int y)
        {
            string tilePath = PathUtils.GetTilePath(this.path, z, x, y);
            if (File.Exists(tilePath))
            {
                byte[] fileBytes = File.ReadAllBytes(tilePath);
                return new Tile(z, x, y, fileBytes);
            }
            else
            {
                return null;
            }
        }
    }
}
