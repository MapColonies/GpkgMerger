using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public abstract class HttpDataSource : Data<IHttpUtils>
    {
        protected TileBounds[] TileRanges;
        protected IEnumerator<Tile[]> Batches;
        protected int BatchIndex = 0;
        protected HttpDataSource(IServiceProvider container,
            DataType type, string path, int batchSize, Extent extent, GridOrigin origin, int maxZoom, int minZoom = 0, bool isOneXOne = false)
            : base(container, type, path, batchSize, isOneXOne, origin)
        {
            //ignore zoom level that cant be converted without image manipulation
            minZoom = isOneXOne ? Math.Max(minZoom, 2) : minZoom;
            this.GenTileRanges(extent, origin, minZoom, maxZoom);
            if (isOneXOne)
            {
                this.FromCurrentGridTile = this.OneXOneConvertor.FromTwoXOne;
                this.ToCurrentGrid = this.OneXOneConvertor.ToTwoXOne;
            }
        }

        public override bool Exists()
        {
            // there is no reasonable way to validate url template source
            // this should be modified if we change the input to received capabilities and layer instead of pattern
            return true;
        }

        public override List<Tile> GetNextBatch(out string batchIdentifier)
        {
            if (this.Batches == null)
            {
                this.Batches = this.GetTiles().Chunk(this.BatchSize).GetEnumerator();
            }
            batchIdentifier = this.BatchIndex.ToString();
            this.BatchIndex += this.BatchSize;
            if (!this.Batches.MoveNext())
            {
                return new List<Tile>(0);
            }

            return this.Batches.Current.ToList();
        }

        public override void Reset()
        {
            this.BatchIndex = 0;
            this.Batches = this.GetTiles().Chunk(this.BatchSize).GetEnumerator();
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this.BatchIndex = int.Parse(batchIdentifier);
            this.Batches = this.GetTiles().Skip(this.BatchIndex).Chunk(this.BatchSize).GetEnumerator();
        }

        public override int TileCount()
        {
            return this.TileRanges.Sum(range => range.Size());
        }

        protected void GenTileRanges(Extent extent, GridOrigin origin, int minZoom, int maxZoom)
        {
            this.TileRanges = new TileBounds[maxZoom - minZoom + 1];
            for (int i = minZoom; i <= maxZoom; i++)
            {
                this.TileRanges[i - minZoom] = GeoUtils.ExtentToTileRange(extent, i, origin);
            }
        }

        protected IEnumerable<Tile> GetTiles()
        {
            foreach (var range in this.TileRanges)
            {
                for (int x = range.MinX; x < range.MaxX; x++)
                {
                    for (int y = range.MinY; y < range.MaxY; y++)
                    {
                        yield return this.GetTile(range.Zoom, x, y);
                    }
                }
            }
        }

        protected override Tile GetOneXOneTile(int z, int x, int y)
        {
            Coord oneXoneBaseCoords = this.OneXOneConvertor.FromTwoXOne(z, x, y);
            Tile tile = this.Utils.GetTile(oneXoneBaseCoords);
            if (tile != null)
            {
                tile.SetCoords(z, x, y);
            }
            return tile;
        }

        protected override void InternalUpdateTiles(IEnumerable<Tile> targetTiles)
        {
            throw new NotImplementedException();
        }

    }
}
