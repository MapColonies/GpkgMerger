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

        public int Size()
        {
            return (this.MaxX - this.MinX) * (this.MaxY - this.MinY);
        }

        public void Print()
        {
            Console.WriteLine($"Zoom: {this.Zoom}");
            Console.WriteLine($"mixX: {this.MinX}");
            Console.WriteLine($"maxX: {this.MaxX}");
            Console.WriteLine($"minY: {this.MinY}");
            Console.WriteLine($"maxY: {this.MaxY}");
        }

        public override string ToString()
        {
            return $"min x: {MinX}, min y: {MinY}, max x: {MaxX}, max y: {MaxY}";
        }
    }
}
