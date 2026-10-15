using System;
using System.Collections.Generic;

namespace PharmaLabVR.UI
{
    // Pure selector for the pinned six-stock Research matrix. Native
    // PrepareStock still verifies the chemistry and material identities.
    public static class ResearchConcentrationSelection
    {
        public static double Resolve(IReadOnlyList<double> supportedMolPerL,
                                     int index, string stockId)
        {
            if (supportedMolPerL == null || index < 0 || index >= supportedMolPerL.Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    "Select a molarity from the installed Research stock catalog.");
            if (string.IsNullOrWhiteSpace(stockId))
                throw new ArgumentException("A selected research stock is required.", nameof(stockId));
            if (stockId == "water") return 0.0;
            var molarity = supportedMolPerL[index];
            if (double.IsNaN(molarity) || double.IsInfinity(molarity) || molarity <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(supportedMolPerL),
                    "An analyte stock requires a finite positive pinned research molarity.");
            return molarity;
        }
    }
}
