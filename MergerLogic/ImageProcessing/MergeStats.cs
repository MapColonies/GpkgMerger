namespace MergerLogic.ImageProcessing
{
    /// <summary>
    /// Describes which inputs contributed to a merged tile, used to classify the
    /// write as added / merged / replaced. TargetUsed is false in upload-only mode.
    /// </summary>
    public readonly struct MergeStats
    {
        public bool TargetUsed { get; }
        public bool AnySourceUsed { get; }

        public MergeStats(bool targetUsed, bool anySourceUsed)
        {
            this.TargetUsed = targetUsed;
            this.AnySourceUsed = anySourceUsed;
        }
    }
}
