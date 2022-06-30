using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public class GeoUtils : IGeoUtils
    {
        public const int SRID = 4326;

        public int FlipY(int z, int y)
        {
            // Convert to and from TMS
            return (1 << z) - y - 1;
        }

        public int FlipY(Coord coord)
        {
            return this.FlipY(coord.Z, coord.Y);
        }

        public int FlipY(Tile tile)
        {
            return this.FlipY(tile.Z, tile.Y);
        }

        public double DegreesPerTile(int zoom)
        {
            double tilesPerYAxis = 1 << zoom; // 2^ zoom
            double tileSizeDeg = 180 / tilesPerYAxis;
            return tileSizeDeg;
        }

        public Extent SnapExtentToTileGrid(Extent extent, int zoom)
        {
            double tileSize = this.DegreesPerTile(zoom);
            double minX = extent.MinX - Math.Abs(extent.MinX % tileSize);
            double minY = extent.MinY - Math.Abs(extent.MinY % tileSize);
            double maxX = extent.MaxX - Math.Abs(extent.MaxX % tileSize);
            double maxY = extent.MaxY - Math.Abs(extent.MaxY % tileSize);
            if (maxX != extent.MaxX)
            {
                maxX += tileSize;
            }
            if (maxY != extent.MaxY)
            {
                maxY += tileSize;
            }
            if (zoom == 0)
            {
                minY = -90;
                maxY = 90;
            }
            return new Extent { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
        }

        public TileBounds ExtentToTileRange(Extent extent, int zoom, GridOrigin origin = GridOrigin.UPPER_LEFT)
        {
            extent = this.SnapExtentToTileGrid(extent, zoom);
            double tileSize = this.DegreesPerTile(zoom);
            double minYDeg = extent.MinY;
            double maxYDeg = extent.MaxY;

            if (origin == GridOrigin.UPPER_LEFT)
            {
                // flip y
                (minYDeg, maxYDeg) = (-maxYDeg, -minYDeg);
            }

            int minX = (int)((extent.MinX + 180) / tileSize);
            int maxX = (int)((extent.MaxX + 180) / tileSize);
            int minY = (int)((minYDeg + 90) / tileSize);
            int maxY = (int)((maxYDeg + 90) / tileSize);

            return new TileBounds(zoom, minX, maxX, minY, maxY);
        }

        public Extent TileRangeToExtent(TileBounds bounds)
        {
            double tileSizeDeg = this.DegreesPerTile(bounds.Zoom);
            double minX = (tileSizeDeg * bounds.MinX) - 180;
            double minY = (tileSizeDeg * bounds.MinY) - 90;
            double maxX = (tileSizeDeg * bounds.MaxX) - 180;
            double maxY = (tileSizeDeg * bounds.MaxY) - 90;
            return new Extent() { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
        }
    }
}
