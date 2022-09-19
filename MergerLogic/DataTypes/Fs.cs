using MergerLogic.Batching;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

namespace MergerLogic.DataTypes
{
    public class FS : Data<IFileClient>
    {
        private IEnumerator<Tile> _tiles;
        private bool _done;
        private long _completedTiles;

        private readonly IPathUtils _pathUtils;
        private readonly IFileSystem _fileSystem;

        public FS(IPathUtils pathUtils, IServiceProvider container,
            string path, int batchSize, Grid? grid, GridOrigin? origin, bool isBase = false)
            : base(container, DataType.FOLDER, path, batchSize, grid, origin)
        {
            this._pathUtils = pathUtils;
            this._fileSystem = container.GetRequiredService<IFileSystem>();
            if (isBase)
            {
                this._fileSystem.Directory.CreateDirectory(path);
            }

            this.Reset();
        }

        protected override GridOrigin DefaultOrigin()
        {
            return GridOrigin.LOWER_LEFT;
        }

        public override void Reset()
        {
            this._tiles = this.GetTiles();
            this._tiles.MoveNext();
            this._done = false;
            this._completedTiles = 0;
        }

        public override bool Exists()
        {
            this._logger.LogInformation($"Checking if exists, folder: {this.Path}");
            string fullPath = this._fileSystem.Path.GetFullPath(this.Path);
            return this._fileSystem.Directory.Exists(fullPath);
        }

        private IEnumerator<Tile> GetTiles()
        {
            // From: https://stackoverflow.com/a/7430971/11915280 and https://stackoverflow.com/a/19961761/11915280
            string[] ext = { ".png", ".jpg", ".jpeg" };
            // Go over directory and count png and jpg files
            foreach (string filePath in this._fileSystem.Directory
                         .EnumerateFiles(this.Path, "*.*", SearchOption.AllDirectories)
                         .Where(file => ext.Any(x => file.EndsWith(x, System.StringComparison.OrdinalIgnoreCase))))
            {
                Coord coord = this._pathUtils.FromPath(filePath, out _);
                Tile tile = this.Utils.GetTile(coord);
                if (tile != null)
                {
                    tile = this.ToCurrentGrid(tile);
                    if (tile != null)
                    {
                        tile = this.ConvertOriginTile(tile);
                        yield return tile;
                    }
                }
            }
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            batchIdentifier = this._completedTiles.ToString();
            List<Tile> tiles = new List<Tile>(this.BatchSize);

            if (this._done)
            {
                this.Reset();
                return tiles;
            }

            while (!this._done && tiles.Count < this.BatchSize)
            {
                Tile tile = this._tiles.Current;
                tiles.Add(tile);
                this._done = !this._tiles.MoveNext();
            }

            this._completedTiles += tiles.Count;

            return tiles;
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this._completedTiles = long.Parse(batchIdentifier);
            // uncomment to make this function work at any point of the run and not only after the source initialization
            //this.tiles.Reset();
            for (long i = 0; i < this._completedTiles; i++)
            {
                this._tiles.MoveNext();
            }
        }

        public override long TileCount()
        {
            // From: https://stackoverflow.com/a/7430971/11915280 and https://stackoverflow.com/a/19961761/11915280
            string[] ext = { ".png", ".jpg", "jpeg" };
            // Go over directory and count png and jpg files
            return this._fileSystem.Directory.EnumerateFiles(this.Path, "*.*", SearchOption.AllDirectories)
                .Count(file => ext.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
        }

        protected override void InternalUpdateTiles(IEnumerable<Tile> targetTiles)
        {
            foreach (Tile tile in targetTiles)
            {
                string tilePath = this._pathUtils.GetTilePath(this.Path, tile);
                byte[] buffer = tile.GetImageBytes();
                using (var ms = new MemoryStream(buffer))
                {
                    var file = this._fileSystem.FileInfo.FromFileName(tilePath);
                    file.Directory.Create();
                    using (Stream fs = file.OpenWrite())
                    {
                        ms.WriteTo(fs);
                    }
                }
            }
        }
    }
}
