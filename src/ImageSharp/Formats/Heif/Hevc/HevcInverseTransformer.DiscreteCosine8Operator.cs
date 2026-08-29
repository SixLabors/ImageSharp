// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Defines the eight-point inverse discrete cosine transform operator.
/// </content>
internal static partial class HevcInverseTransformer
{
    /// <summary>
    /// Implements the eight-point inverse discrete cosine transform.
    /// </summary>
    private readonly struct DiscreteCosine8Operator : IHevcInverseTransformOperator
    {
        /// <inheritdoc/>
        public static int Size => 8;

        /// <inheritdoc/>
        public static bool UsesButterfly => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCoefficient(int frequency, int position)
        {
            if (frequency == 0)
            {
                return 64;
            }

            int angle = ((2 * position) + 1) * frequency * 4;
            angle &= 127;
            if (angle > 64)
            {
                angle = 128 - angle;
            }

            // The second quadrant reuses the first-quadrant magnitude with a negative sign.
            if (angle > 32)
            {
                return -DiscreteCosineMagnitudes[64 - angle];
            }

            return DiscreteCosineMagnitudes[angle];
        }
    }
}
