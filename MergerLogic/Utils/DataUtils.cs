using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
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
            return this.GetTile(coord.z, coord.x, coord.y);
        }

        public abstract bool TileExists(int z, int x, int y);
    }
}
