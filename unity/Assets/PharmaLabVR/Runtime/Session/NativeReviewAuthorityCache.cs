using System;

namespace PharmaLabVR.Session
{
    // UI-only recorded caches, never a source of Native scientific facts.
    // Restoring a checkpoint or importing a session changes the authority
    // even if Unity retains the same CoreDriver and Native sequence strings.
    public sealed class NativeReviewAuthorityCache<TReview, TComparison>
        where TReview : class
        where TComparison : class
    {
        private object boundDriver;
        private ulong boundEpoch;
        private bool observed;

        public TReview Review { get; set; }
        public TComparison Comparison { get; set; }

        public void Observe(object driver, ulong restorationEpoch)
        {
            if (observed && ReferenceEquals(boundDriver, driver) &&
                boundEpoch == restorationEpoch)
                return;

            // First observation establishes authority before any data exists.
            // Later changes invalidate both recorded-only representations.
            if (observed)
            {
                Review = null;
                Comparison = null;
            }

            boundDriver = driver;
            boundEpoch = restorationEpoch;
            observed = true;
        }
    }
}
