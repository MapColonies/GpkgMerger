using System.Text.Json.Serialization;

namespace MergerService.Controllers
{
    public class Bounds
    {
        public int Zoom { get; }

        public int MinX { get; }

        public int MaxX { get; }

        public int MinY { get; }

        public int MaxY { get; }

        public Bounds(int zoom, int minX, int maxX, int minY, int maxY)
        {
            this.Zoom = zoom;
            this.MinX = minX;
            this.MaxX = maxX;
            this.MinY = minY;
            this.MaxY = maxY;
        }

        public int Size()
        {
            return (MaxX - MinX) * (MaxY - MinY);
        }

        public void Print()
        {
            Console.WriteLine($"Zoom: {Zoom}");
            Console.WriteLine($"mixX: {MinX}");
            Console.WriteLine($"maxX: {MaxX}");
            Console.WriteLine($"minY: {MinY}");
            Console.WriteLine($"maxY: {MaxY}");
        }
    }
}
