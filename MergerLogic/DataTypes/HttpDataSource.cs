using MergerLogic.Batching;
using MergerLogic.Utils;

namespace MergerLogic.DataTypes
{
    public abstract class HttpDataSource : Data<IHttpUtils>
    {
        protected TileBounds[] tileRanges;
        protected IEnumerator<Tile[]> batches;
        protected int batchIndex = 0;
        protected HttpDataSource(IServiceProvider container,
            DataType type, string path, int batchSize, Extent extent, GridOrigin origin, int maxZoom, int minZoom = 0, bool isOneXOne = false)
            : base(container, type, path, batchSize, isOneXOne, origin)
        {
            var patternUtils = new PathPatternUtils(path);
            this.utils = new HttpUtils(path, patternUtils);
            //ignore zoom level that cant be converted without image manipulation
            minZoom = isOneXOne ? Math.Max(minZoom, 2) : minZoom;
            this.GenTileRanges(extent, origin, minZoom, maxZoom);
            if (isOneXOne)
            {
                this._fromCurrentGridTile = this._oneXOneConvetor.FromTwoXOne;
                this._toCurrentGrid = this._oneXOneConvetor.ToTwoXOne;
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
            if (this.batches == null)
            {
                this.batches = this.GetTiles().Chunk(this.batchSize).GetEnumerator();
            }
            batchIdentifier = this.batchIndex.ToString();
            this.batchIndex += this.batchSize;
            if (!this.batches.MoveNext())
            {
                return new List<Tile>(0);
            }

            return this.batches.Current.ToList();
        }

        public override void Reset()
        {
            this.batchIndex = 0;
            this.batches = this.GetTiles().Chunk(this.batchSize).GetEnumerator();
        }

        public override void setBatchIdentifier(string batchIdentifier)
        {
            this.batchIndex = int.Parse(batchIdentifier);
            this.batches = this.GetTiles().Skip(this.batchIndex).Chunk(this.batchSize).GetEnumerator();
        }

        public override int TileCount()
        {
            return this.tileRanges.Sum(range => range.Size());
        }

        protected void GenTileRanges(Extent extent, GridOrigin origin, int minZoom, int maxZoom)
        {
            this.tileRanges = new TileBounds[maxZoom - minZoom + 1];
            for (int i = minZoom; i <= maxZoom; i++)
            {
                this.tileRanges[i - minZoom] = GeoUtils.ExtentToTileRange(extent, i, origin);
            }
        }

        protected IEnumerable<Tile> GetTiles()
        {
            foreach (var range in this.tileRanges)
            {
                for (int x = range.MinX; x < range.MaxX; x++)
                {
                    for (int y = range.MinY; y < range.MaxY; y++)
                    {
                        yield return this._getTile(range.Zoom, x, y);
                    }
                }
            }
        }

        protected override Tile GetOneXOneTile(int z, int x, int y)
        {
            Coord oneXoneBaseCoords = this._oneXOneConvetor.FromTwoXOne(z, x, y);
            Tile tile = this.utils.GetTile(oneXoneBaseCoords);
            if (tile == null)
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
