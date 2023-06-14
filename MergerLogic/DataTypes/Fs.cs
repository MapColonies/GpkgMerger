using MergerLogic.Batching;
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
        private IFileSystem _fileSystem;

        private readonly string[] _supportedFileExtensions = { ".png", ".jpg", ".jpeg" };
        static readonly object _locker = new object();

        public FS(IPathUtils pathUtils, IServiceProvider container,
            string path, int batchSize, Grid? grid, GridOrigin? origin, bool isBase = false)
            : base(container, DataType.FOLDER, path, batchSize, grid, origin, isBase)
        {
            this._pathUtils = pathUtils;
            this.Reset();
        }

        protected override void Initialize()
        {
            this._fileSystem = this._container.GetRequiredService<IFileSystem>();
        }

        protected override void Create() {
            this._fileSystem.Directory.CreateDirectory(this.Path);
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

        private IEnumerable<int> GetZoomLevels()
        {
            List<int> zoomLevels = new List<int>();

            // Get all sub-directories which represent zoom levels
            foreach (string dirPath in this._fileSystem.Directory.GetDirectories(this.Path))
            {
                FileInfo fileInfo = new FileInfo(dirPath);
                if (int.TryParse(fileInfo.Name, out int zoom))
                {
                    zoomLevels.Add(zoom);
                }
            }

            // Return zoom levels in ASC order
            zoomLevels.Sort();
            return zoomLevels;
        }

        private IEnumerator<Tile> GetTiles()
        {
            // From: https://stackoverflow.com/a/7430971/11915280 and https://stackoverflow.com/a/19961761/11915280
            IEnumerable<int> zoomLevels = this.GetZoomLevels();

            foreach (int zoomLevel in zoomLevels)
            {
                string path = $"{this.Path}{this._fileSystem.Path.DirectorySeparatorChar}{zoomLevel}";

                // Go over directory and count png and jpg files
                foreach (string filePath in this._fileSystem.Directory
                            .EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                            .Where(file => this._supportedFileExtensions.Any(x => file.EndsWith(x, System.StringComparison.OrdinalIgnoreCase))))
                {
                    Coord coord = this._pathUtils.FromPath(filePath, out _);
                    Tile? tile = this.Utils.GetTile(coord);
                    if (tile is null)
                    {
                        continue;
                    }

                    tile = this.ToCurrentGrid(tile);
                    if (tile is null)
                    {
                        continue;
                    }

                    tile = this.ConvertOriginTile(tile);
                    yield return tile;
                }
            }
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier,out string? nextBatchIdentifier, long? totalTilesCount)
        {
            lock (_locker)
            {
                batchIdentifier = this._completedTiles.ToString();
                List<Tile> tiles = new List<Tile>(this.BatchSize);
                nextBatchIdentifier = batchIdentifier;
                
                if (this._done)
                {
                    return tiles;
                }
                
                while (!this._done && tiles.Count < this.BatchSize)
                {
                    Tile tile = this._tiles.Current;
                    tiles.Add(tile);
                    this._done = !this._tiles.MoveNext();
                }
                Interlocked.Add(ref _completedTiles, tiles.Count);
                nextBatchIdentifier = this._completedTiles.ToString();
                return tiles;
            }
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            lock (_locker)
            {
                this._tiles = this.GetTiles();
                this._tiles.MoveNext();
                _completedTiles = long.Parse(batchIdentifier);
                
                for (long i = 0; i < this._completedTiles; i++)
                {
                    this._tiles.MoveNext();
                }
            }
        }

        public override long TileCount()
        {
            // From: https://stackoverflow.com/a/7430971/11915280 and https://stackoverflow.com/a/19961761/11915280
            IEnumerable<int> zoomLevels = this.GetZoomLevels();
            long count = 0;

            foreach (int zoomLevel in zoomLevels)
            {
                string path = $"{this.Path}{this._fileSystem.Path.DirectorySeparatorChar}{zoomLevel}";

                // Go over directory and count png and jpg files
                count += this._fileSystem.Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                    .LongCount(file => this._supportedFileExtensions.Any(x => file.EndsWith(x, StringComparison.OrdinalIgnoreCase)));
            }

            return count;
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
