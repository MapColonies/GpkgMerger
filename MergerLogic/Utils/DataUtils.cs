using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;

namespace MergerLogic.Utils
{
    public abstract class DataUtils : IDataUtils
    {
        protected readonly string path;
        protected readonly IGeoUtils GeoUtils;
        protected readonly IImageFormatter Formatter;

        public DataUtils(string path, IGeoUtils geoUtils, IImageFormatter formatter)
        {
            this.path = path;
            this.GeoUtils = geoUtils;
            this.Formatter = formatter;
        }

        public virtual bool IsValidGrid(bool isOneXOne = false) {
            return true;
        }

        public abstract Tile? GetTile(int z, int x, int y);

        public virtual Tile? GetTile(Coord coord)
        {
            return this.GetTile(coord.Z, coord.X, coord.Y);
        }

        public abstract bool TileExists(int z, int x, int y);

        protected Tile? createTile(int z, int x, int y, byte[]? data)
        {
            if (data == null)
            {
                return null;
            }

            var format = this.Formatter.GetTileFormat(data);
            return new Tile(z, x, y, data, format);
        }
    }
}
