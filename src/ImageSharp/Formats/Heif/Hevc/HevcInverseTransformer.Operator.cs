// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Defines the HEVC inverse-transform operator contract.
/// </content>
internal static partial class HevcInverseTransformer
{
    /// <summary>
    /// Defines one closed inverse-transform operation selected by the transform-unit syntax.
    /// </summary>
    private interface IHevcInverseTransformOperator
    {
        /// <summary>
        /// Gets the transform side in samples.
        /// </summary>
        public static abstract int Size { get; }

        /// <summary>
        /// Gets a value indicating whether the transform uses the partial-butterfly factorization.
        /// </summary>
        public static abstract bool UsesButterfly { get; }

        /// <summary>
        /// Gets one inverse-transform matrix coefficient.
        /// </summary>
        /// <param name="frequency">The frequency-domain coordinate.</param>
        /// <param name="position">The spatial-domain coordinate.</param>
        /// <returns>The signed transform coefficient.</returns>
        public static abstract int GetCoefficient(int frequency, int position);
    }
}
