// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;

internal partial class LutABCalculator
{
    /// <summary>
    /// Identifies the transform direction for the configured LUT calculator.
    /// </summary>
    private enum CalculationType
    {
        /// <summary>
        /// Converts from device space to PCS using ICC <c>mAB</c> stage order.
        /// </summary>
        AtoB,

        /// <summary>
        /// Converts from PCS to device space using ICC <c>mBA</c> stage order.
        /// </summary>
        BtoA,
    }
}
