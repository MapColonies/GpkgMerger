using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public static class GeoUtils
    {
        public static int FlipY(int z, int y)
        {
            // Convert to and from TMS
            return (1 << z) - y - 1;
        }

        public static int FlipY(Coord coord)
        {
            return FlipY(coord.z, coord.y);
        }

        public static int FlipY(Tile tile)
        {
            return FlipY(tile.Z, tile.Y);
        }

        public static double DegreesPerTile(int zoom)
        {
            double tilesPerYAxis = 1 << zoom; // 2^ zoom
            double tileSizeDeg = 180 / tilesPerYAxis;
            return tileSizeDeg;
        }

        public static Extent SnapExtentToTileGrid(Extent extent, int zoom)
        {
            double tileSize = DegreesPerTile(zoom);
            double minX = extent.minX - Math.Abs(extent.minX % tileSize);
            double minY = extent.minY - Math.Abs(extent.minY % tileSize);
            double maxX = extent.maxX - Math.Abs(extent.maxX % tileSize);
            double maxY = extent.maxY - Math.Abs(extent.maxY % tileSize);
            if (maxX != extent.maxX)
            {
                maxX += tileSize;
            }
            if (maxY != extent.maxY)
            {
                maxY += tileSize;
            }
            if (zoom == 0)
            {
                minY = -90;
                maxY = 90;
            }
            return new Extent { minX = minX, minY = minY, maxX = maxX, maxY = maxY };
        }

        public static Bounds ExtentToTileRange(Extent extent, int zoom, GridOrigin origin = GridOrigin.UPPER_LEFT)
        {
            extent = SnapExtentToTileGrid(extent, zoom);
            double tileSize = DegreesPerTile(zoom);
            double minYDeg = extent.minY;
            double maxYDeg = extent.maxY;

            if (origin == GridOrigin.UPPER_LEFT)
            {
                // flip y
                (minYDeg, maxYDeg) = (-maxYDeg, -minYDeg);
            }

            int minX = (int)((extent.minX + 180) / tileSize);
            int maxX = (int)((extent.maxX + 180) / tileSize);
            int minY = (int)((minYDeg + 90) / tileSize);
            int maxY = (int)((maxYDeg + 90) / tileSize);

            return new Bounds(zoom, minX, maxX, minY, maxY);
        }
    }
}
