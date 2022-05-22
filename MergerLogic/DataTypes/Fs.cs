using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public class FS : Data
    {
        private IEnumerator<Tile> tiles;
        private bool done;
        private int completedTiles;

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
            this.completedTiles = 0;
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
                coord.flipY();
                Tile tile = this.utils.GetTile(coord);
                yield return tile;
            }
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            batchIdentifier = this.completedTiles.ToString();
            List<Tile> tiles = new List<Tile>(this.batchSize);

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

            this.completedTiles += tiles.Count;

            return tiles;
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this.completedTiles = int.Parse(batchIdentifier);
            // uncomment to make this function work at any point of the run and not only after the source initialization
            //this.tiles.Reset();
            for (int i = 0; i < this.completedTiles; i++)
            {
                this.tiles.MoveNext();
            }
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