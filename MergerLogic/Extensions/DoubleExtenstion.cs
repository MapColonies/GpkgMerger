namespace MergerLogic.Extensions
{
    public static class DoubleExtenstion
    {
        public static bool IsApproximatelyEqualTo(this double initialValue, double value, double maximumDifferenceAllowed)
        {
            // Handle comparisons of floating point values that may not be exactly the same
            return (Math.Abs(initialValue - value) <= maximumDifferenceAllowed);
        }
    }
}
