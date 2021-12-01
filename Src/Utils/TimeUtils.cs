using System;

namespace GpkgMerger.Src.Utils
{
    public static class TimeUtils
    {
        public static void PrintElapsedTime(string prompt, TimeSpan ts)
        {
            // Format and display the TimeSpan value.
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            Console.WriteLine($"{prompt}: {elapsedTime}");
        }
    }
}