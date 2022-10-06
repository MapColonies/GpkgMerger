using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.ImageProcessing;
using System.IO.Abstractions;

namespace MergerLogic.Utils
{
    public class PathUtils : IPathUtils
    {
        private readonly IFileSystem _fileSystem;
        private readonly IImageFormatter _imageFormatter;

        public PathUtils(IFileSystem fileSystem, IImageFormatter formatter)
        {
            this._fileSystem = fileSystem;
            this._imageFormatter = formatter;
        }

        public string RemoveTrailingSlash(string path, bool isS3 = false)
        {
            return path.TrimEnd(this.GetSeparator(isS3));
        }

        public string GetTilePathWithoutExtension(string basePath, int z, int x, int y, bool isS3 = false)
        {
            char separator = this.GetSeparator(isS3);
            return $"{basePath}{separator}{z}{separator}{x}{separator}{y}.";
        }

        public string GetTilePath(string basePath, Tile tile, bool isS3 = false)
        {
            var format = tile.Format ?? this._imageFormatter.GetTileFormat(tile.GetImageBytes());
            return this.GetTilePath(basePath, tile.Z, tile.X, tile.Y, format!.Value, isS3);
        }

        public string GetTilePath(string basePath, int z, int x, int y, TileFormat format, bool isS3 = false)
        {
            return $"{this.GetTilePathWithoutExtension(basePath, z, x, y, isS3)}{format.ToString().ToLower()}";
        }

        public Coord FromPath(string path, out TileFormat format, bool isS3 = false)
        {
            string[] parts = path.Split(this.GetSeparator(isS3));
            int numParts = parts.Length;

            // Each path represents a tile, therefore the last three parts represent the z, x and y values
            string[] last = parts[numParts - 1].Split('.');
            int z = int.Parse(parts[numParts - 3]);
            int x = int.Parse(parts[numParts - 2]);
            int y = int.Parse(last[0]);
            if (last[1].ToLower() == "jpg")
            {
                format = TileFormat.Jpeg;
            }
            else
            {
                format = (TileFormat)Enum.Parse(typeof(TileFormat), last[1], true);
            }

            return new Coord(z, x, y);
        }

        private char GetSeparator(bool isS3)
        {
            return isS3 ? '/' : this._fileSystem.Path.DirectorySeparatorChar;
        }
    }
}
