using MergerLogic.Batching;

namespace MergerLogic.Utils
{
    public class FileUtils : DataUtils, IFileUtils
    {
        private IPathUtils _pathUtils;

        public FileUtils(string path, IPathUtils pathUtils, IGeoUtils geoUtils) : base(path, geoUtils)
        {
            this._pathUtils = pathUtils;
        }

        public override Tile GetTile(int z, int x, int y)
        {
            string tilePath = this._pathUtils.GetTilePath(this.path, z, x, y);
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

        public override bool TileExists(int z, int x, int y)
        {
            string fullPath = this._pathUtils.GetTilePath(this.path, z, x, y);
            return File.Exists(fullPath);
        }
    }
}
