using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public abstract class DataUtils : IDataUtils
    {
        protected readonly string path;
        protected readonly IGeoUtils GeoUtils;

        public DataUtils(string path, IGeoUtils geoUtils)
        {
            this.path = path;
            this.GeoUtils = geoUtils;
        }

        public abstract Tile? GetTile(int z, int x, int y);

        public virtual Tile? GetTile(Coord coord)
        {
            return this.GetTile(coord.Z, coord.X, coord.Y);
        }

        public abstract bool TileExists(int z, int x, int y);
    }
}
