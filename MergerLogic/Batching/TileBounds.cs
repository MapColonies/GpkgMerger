namespace MergerLogic.Batching
{
    public class TileBounds
    {
        public int Zoom { get; }

        public int MinX { get; }

        public int MaxX { get; }

        public int MinY { get; }

        public int MaxY { get; }

        public TileBounds(int zoom, int minX, int maxX, int minY, int maxY)
        {
            this.Zoom = zoom;
            this.MinX = minX;
            this.MaxX = maxX;
            this.MinY = minY;
            this.MaxY = maxY;
        }

        public long Size()
        {
            return (this.MaxX - this.MinX) * (this.MaxY - this.MinY);
        }

        public void Print()
        {
            Console.WriteLine(this.ToString());
        }

        public override string ToString()
        {
            return $"zoom: {this.Zoom}, min x: {this.MinX}, min y: {this.MinY}, max x: {this.MaxX}, max y: {this.MaxY}";
        }
    }
}
