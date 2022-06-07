using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public class PathUtils : IPathUtils
    {
        public string RemoveTrailingSlash(string path, bool isS3 = false)
        {
            return path.TrimEnd(GetSeperator(isS3));
        }

        public string GetTilePath(string basePath, Tile tile)
        {
            return GetTilePath(basePath, tile.Z, tile.X, tile.Y);
        }

        public string GetTilePath(string basePath, int z, int x, int y, bool isS3 = false)
        {
            char seperator = GetSeperator(isS3);
            return $"{basePath}{seperator}{z}{seperator}{x}{seperator}{y}.png";
        }

        public string GetTilePathTMS(string basePath, int z, int x, int y, bool isS3 = false)
        {
            y = GeoUtils.FlipY(z, y);
            return GetTilePath(basePath, z, x, y, isS3);
        }

        public Coord FromPath(string path, bool isS3 = false)
        {
            string[] parts = path.Split(GetSeperator(isS3));
            int numParts = parts.Length;

            // Each path represents a tile, therefore the last three parts represent the z, x and y values
            string[] last = parts[numParts - 1].Split('.');
            int z = int.Parse(parts[numParts - 3]);
            int x = int.Parse(parts[numParts - 2]);
            int y = int.Parse(last[0]);

            return new Coord(z, x, y);
        }

        private char GetSeperator(bool isS3)
        {
            return isS3 ? '/' : Path.DirectorySeparatorChar;
        }
    }
}
