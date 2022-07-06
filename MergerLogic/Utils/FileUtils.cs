using MergerLogic.Batching;
using System.IO.Abstractions;

namespace MergerLogic.Utils
{
    public class FileUtils : DataUtils, IFileUtils
    {
        private readonly IPathUtils _pathUtils;
        private readonly IFileSystem _fileSystem;

        public FileUtils(string path, IPathUtils pathUtils, IGeoUtils geoUtils, IFileSystem fileSystem) : base(path, geoUtils)
        {
            this._pathUtils = pathUtils;
            this._fileSystem = fileSystem;
        }

        public override Tile GetTile(int z, int x, int y)
        {
            string tilePath = this._pathUtils.GetTilePath(this.path, z, x, y);
            if (this._fileSystem.File.Exists(tilePath))
            {
                byte[] fileBytes = this._fileSystem.File.ReadAllBytes(tilePath);
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
            return this._fileSystem.File.Exists(fullPath);
        }
    }
}
