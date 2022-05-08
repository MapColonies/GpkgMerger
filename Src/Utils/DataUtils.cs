using GpkgMerger.Src.Batching;
using GpkgMerger.Src.DataTypes;

namespace GpkgMerger.Src.Utils
{
    public abstract class DataUtils
    {
        protected readonly string path;

        public DataUtils(string path)
        {
            this.path = path;
        }

        public abstract Tile GetTile(int z, int x, int y);

        public virtual Tile GetTile(Coord coord)
        {
            return GetTile(coord.z, coord.x, coord.y);
        }
    }
}
