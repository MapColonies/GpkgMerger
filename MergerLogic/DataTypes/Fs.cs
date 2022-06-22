using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class FS : Data<IFileUtils>
    {
        private delegate string TilePathFunction(string path, Tile tile);

        private IEnumerator<Tile> _tiles;
        private bool _done;
        private int _completedTiles;

        private IPathUtils _pathUtils;

        public FS(IPathUtils pathUtils, IServiceProvider container,
            string path, int batchSize, bool isOneXOne = false, bool isBase = false, GridOrigin origin = GridOrigin.LOWER_LEFT)
            : base(container, DataType.FOLDER, path, batchSize, isOneXOne, origin)
        {
            this._pathUtils = pathUtils;
            if (isBase)
            {
                Directory.CreateDirectory(path);
            }
            this.Reset();
        }

        public override void Reset()
        {
            this._tiles = this.GetTiles();
            this._tiles.MoveNext();
            this._done = false;
            this._completedTiles = 0;
        }

        public override void Wrapup()
        {
            base.Wrapup();
            this.Reset();
        }

        public override bool Exists()
        {
            Console.WriteLine($"Checking if exists, folder: {this.Path}");
            string fullPath = System.IO.Path.GetFullPath(this.Path);
            return Directory.Exists(fullPath);
        }

        private IEnumerator<Tile> GetTiles()
        {
            // From: https://stackoverflow.com/a/7430971/11915280 and https://stackoverflow.com/a/19961761/11915280
            string[] ext = { ".png", ".jpg" };
            // Go over directory and count png and jpg files
            foreach (string filePath in Directory.EnumerateFiles(this.Path, "*.*", SearchOption.AllDirectories)
                                                    .Where(file => ext.Any(x => file.EndsWith(x, System.StringComparison.OrdinalIgnoreCase))))
            {
                Coord coord = this._pathUtils.FromPath(filePath);
                Tile tile = this.utils.GetTile(coord);
                if (tile != null)
                {
                    tile = this._toCurrentGrid(tile);
                    if (tile != null)
                    {
                        tile = this._convertOriginTile(tile);
                        yield return tile;
                    }
                }
            }
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            batchIdentifier = this._completedTiles.ToString();
            List<Tile> tiles = new List<Tile>(this.batchSize);

            if (this._done)
            {
                this.Reset();
                return tiles;
            }

            while (!this._done && tiles.Count < this.batchSize)
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
            this._completedTiles = int.Parse(batchIdentifier);
            // uncomment to make this function work at any point of the run and not only after the source initialization
            //this.tiles.Reset();
            for (int i = 0; i < this._completedTiles; i++)
            {
                this._tiles.MoveNext();
            }
        }

        public override int TileCount()
        {
            // From: https://stackoverflow.com/a/7430971/11915280 and https://stackoverflow.com/a/19961761/11915280
            string[] ext = { ".png", ".jpg" };
            // Go over directory and count png and jpg files
            return Directory.EnumerateFiles(this.Path, "*.*", SearchOption.AllDirectories).Where(file => ext.Any(x => file.EndsWith(x, System.StringComparison.OrdinalIgnoreCase))).Count();
        }

        protected override void InternalUpdateTiles(IEnumerable<Tile> targetTiles)
        {
            foreach (Tile tile in targetTiles)
            {
                string tilePath = this._pathUtils.GetTilePath(this.Path, tile);
                byte[] buffer = tile.GetImageBytes();
                using (var ms = new MemoryStream(buffer))
                {
                    var file = new System.IO.FileInfo(tilePath);
                    if (file.Directory != null)
                        file.Directory.Create();
                    using (FileStream fs = file.OpenWrite())
                    {
                        ms.WriteTo(fs);
                    }
                }
            }
        }
    }
}
