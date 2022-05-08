using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GpkgMerger.Src.Batching;
using GpkgMerger.Src.Utils;

namespace GpkgMerger.Src.DataTypes
{
    public class FS : Data
    {
        private IEnumerator<Tile> tiles;
        private bool done;

        public FS(DataType type, string path, int batchSize) : base(type, path, batchSize, new FileUtils(path))
        {
            this.tiles = GetTiles();
            this.tiles.MoveNext();
            done = false;
        }

        public override bool Exists()
        {
            Console.WriteLine($"Checking if exists, folder: {this.path}");
            string fullPath = Path.GetFullPath(this.path);
            return Directory.Exists(fullPath);
        }

        private IEnumerator<Tile> GetTiles()
        {
            // From: https://stackoverflow.com/a/7430971/11915280 and https://stackoverflow.com/a/19961761/11915280
            string[] ext = { ".png", ".jpg" };
            // Go over directory and count png and jpg files
            foreach (string filePath in Directory.EnumerateFiles(this.path, "*.*", SearchOption.AllDirectories).Where(file => ext.Any(x => file.EndsWith(x, System.StringComparison.OrdinalIgnoreCase))))
            {
                Coord coord = PathUtils.FromPath(filePath);
                Tile tile = this.utils.GetTile(coord);
                yield return tile;
            }
        }

        public override List<Tile> GetNextBatch()
        {
            List<Tile> tiles = new List<Tile>();

            if (done)
            {
                this.tiles = GetTiles();
                done = false;
                return tiles;
            }

            while (!done && tiles.Count < this.batchSize)
            {
                Tile tile = this.tiles.Current;
                tiles.Add(tile);
                done = !this.tiles.MoveNext();
            }

            return tiles;
        }

        public override int TileCount()
        {
            // From: https://stackoverflow.com/a/7430971/11915280 and https://stackoverflow.com/a/19961761/11915280
            string[] ext = { ".png", ".jpg" };
            // Go over directory and count png and jpg files
            return Directory.EnumerateFiles(this.path, "*.*", SearchOption.AllDirectories).Where(file => ext.Any(x => file.EndsWith(x, System.StringComparison.OrdinalIgnoreCase))).Count();
        }

        public override void UpdateTiles(List<Tile> tiles)
        {
            foreach (Tile tile in tiles)
            {
                string tilePath = PathUtils.GetTilePath(this.path, tile);
                byte[] buffer = StringUtils.StringToByteArray(tile.Blob);
                using (var ms = new MemoryStream(buffer))
                using (var fs = File.OpenWrite(tilePath))
                {
                    ms.WriteTo(fs);
                }
            }
        }
    }
}
