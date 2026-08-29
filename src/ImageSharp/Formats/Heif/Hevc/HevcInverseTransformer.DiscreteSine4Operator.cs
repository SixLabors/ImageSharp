// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Defines the four-point inverse discrete sine transform operator.
/// </content>
internal static partial class HevcInverseTransformer
{
    /// <summary>
    /// Implements the four-point inverse discrete sine transform.
    /// </summary>
    private readonly struct DiscreteSine4Operator : IHevcInverseTransformOperator
    {
        /// <summary>
        /// Gets the inverse-DST matrix in frequency-major order.
        /// </summary>
        private static ReadOnlySpan<sbyte> Coefficients =>
        [
            29, 55, 74, 84,
            74, 74, 0, -74,
            84, -29, -74, 55,
            55, -84, 74, -29
        ];

        /// <inheritdoc/>
        public static int Size => 4;

        /// <inheritdoc/>
        public static bool UsesButterfly => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCoefficient(int frequency, int position)
        {
            return Coefficients[(frequency * Size) + position];
        }
    }
}
