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
        private readonly IConfigurationManager _configurationManager;

        public DataUtils(string path, IGeoUtils geoUtils, IConfigurationManager configuration)
        {
            this.path = path;
            this.GeoUtils = geoUtils;
            this._configurationManager = configuration;
        }

        public virtual bool IsValidGrid(bool isOneXOne = false)
        {
            return true;
        }

        public abstract Tile? GetTile(int z, int x, int y);

        public virtual Tile? GetTile(Coord coord)
        {
            return this.GetTile(coord.Z, coord.X, coord.Y);
        }

        public abstract bool TileExists(int z, int x, int y);

        protected Tile? CreateTile(int z, int x, int y, byte[]? data)
        {
            if (data == null)
            {
                return null;
            }

            return new Tile(this._configurationManager, z, x, y, data);
        }

        protected Tile? CreateTile(Coord coord, byte[]? data)
        {
            return this.CreateTile(coord.Z, coord.X, coord.Y, data);
        }
    }
}
