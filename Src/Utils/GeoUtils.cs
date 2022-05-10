using GpkgMerger.Src.Batching;

namespace GpkgMerger.Src.Utils
{
    public static class GeoUtils
    {
        public static int FlipY(int z, int y)
        {
            // Convert to and from TMS
            return (1 << z) - y - 1;
        }

        public static int convertTMS(Tile tile)
        {
            return FlipY(tile.Z, tile.Y);
        }
    }
}
