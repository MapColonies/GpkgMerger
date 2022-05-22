using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class FS : Data
    {
        private IEnumerator<Tile> tiles;
        private bool done;

        public FS(DataType type, string path, int batchSize, bool isBase = false) : base(type, path, batchSize, new FileUtils(path))
        {
            if (isBase)
            {
                Directory.CreateDirectory(path);
            }
            this.Reset();
        }

        public override void Reset()
        {
            this.tiles = this.GetTiles();
            this.tiles.MoveNext();
            this.done = false;
        }

        public override void Wrapup()
        {
            base.Wrapup();
            this.Reset();
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
            foreach (string filePath in Directory.EnumerateFiles(this.path, "*.*", SearchOption.AllDirectories)
                                                    .Where(file => ext.Any(x => file.EndsWith(x, System.StringComparison.OrdinalIgnoreCase))))
            {
                Coord coord = PathUtils.FromPath(filePath);
                Tile tile = this.utils.GetTile(coord);
                yield return tile;
            }
        }

        public override List<Tile> GetNextBatch()
        {
            List<Tile> tiles = new List<Tile>();

            if (this.done)
            {
                this.Reset();
                return tiles;
            }

            while (!this.done && tiles.Count < this.batchSize)
            {
                Tile tile = this.tiles.Current;
                tiles.Add(tile);
                this.done = !this.tiles.MoveNext();
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
                tile.FlipY();
                string tilePath = PathUtils.GetTilePath(this.path, tile);
                byte[] buffer = StringUtils.StringToByteArray(tile.Blob);
                using (var ms = new MemoryStream(buffer))
                {
                    var file = new System.IO.FileInfo(tilePath);
                    if(file.Directory != null)
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
