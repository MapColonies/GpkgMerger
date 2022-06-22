

namespace MergerLogic.Utils
{
    public class TimeUtils : ITimeUtils
    {
        public string FormatElapsedTime(string prompt, TimeSpan ts)
        {
            // Format and display the TimeSpan value.
            string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            return $"{prompt}: {elapsedTime}";
        }
    }
}
