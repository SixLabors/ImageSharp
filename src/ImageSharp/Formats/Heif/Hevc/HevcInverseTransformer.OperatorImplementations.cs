// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

internal static partial class HevcInverseTransformer
{
    /// <summary>
    /// Gets the common HEVC inverse-DCT magnitudes ordered on the pi-over-sixty-four angle grid.
    /// </summary>
    private static ReadOnlySpan<sbyte> DiscreteCosineMagnitudes =>
    [
        90, 90, 90, 90, 89, 88, 87, 85, 83, 82, 80, 78, 75, 73, 70, 67, 64,
        61, 57, 54, 50, 46, 43, 38, 36, 31, 25, 22, 18, 13, 9, 4, 0
    ];

    /// <summary>
    /// Gets the four-point HEVC inverse-DST matrix in frequency-major order.
    /// </summary>
    private static ReadOnlySpan<sbyte> DiscreteSine4Coefficients =>
    [
        29, 55, 74, 84,
        74, 74, 0, -74,
        84, -29, -74, 55,
        55, -84, 74, -29
    ];

    /// <summary>
    /// Reconstructs one normative inverse-DCT coefficient from the common HEVC angle grid.
    /// </summary>
    /// <param name="size">The transform side.</param>
    /// <param name="frequency">The frequency-domain coordinate.</param>
    /// <param name="position">The spatial-domain coordinate.</param>
    /// <returns>The signed transform coefficient.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetDiscreteCosineCoefficient(int size, int frequency, int position)
    {
        if (frequency == 0)
        {
            return 64;
        }

        int angle = ((2 * position) + 1) * frequency * (32 / size);
        angle &= 127;
        if (angle > 64)
        {
            angle = 128 - angle;
        }

        // Angles in the second quadrant reuse the first-quadrant magnitude with a negative sign. Keeping the
        // normative integer magnitudes in one compile-time span avoids runtime trigonometry and duplicate matrices.
        if (angle > 32)
        {
            return -DiscreteCosineMagnitudes[64 - angle];
        }

        return DiscreteCosineMagnitudes[angle];
    }

    /// <summary>
    /// Implements the four-point inverse discrete cosine transform.
    /// </summary>
    private readonly struct DiscreteCosine4Operator : IHevcInverseTransformOperator<DiscreteCosine4Operator>
    {
        /// <inheritdoc/>
        public static int Size => 4;

        /// <inheritdoc/>
        public static bool UsesButterfly => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCoefficient(int frequency, int position) => GetDiscreteCosineCoefficient(Size, frequency, position);
    }

    /// <summary>
    /// Implements the four-point inverse discrete sine transform.
    /// </summary>
    private readonly struct DiscreteSine4Operator : IHevcInverseTransformOperator<DiscreteSine4Operator>
    {
        /// <inheritdoc/>
        public static int Size => 4;

        /// <inheritdoc/>
        public static bool UsesButterfly => false;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCoefficient(int frequency, int position) => DiscreteSine4Coefficients[(frequency * Size) + position];
    }

    /// <summary>
    /// Implements the eight-point inverse discrete cosine transform.
    /// </summary>
    private readonly struct DiscreteCosine8Operator : IHevcInverseTransformOperator<DiscreteCosine8Operator>
    {
        /// <inheritdoc/>
        public static int Size => 8;

        /// <inheritdoc/>
        public static bool UsesButterfly => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCoefficient(int frequency, int position) => GetDiscreteCosineCoefficient(Size, frequency, position);
    }

    /// <summary>
    /// Implements the sixteen-point inverse discrete cosine transform.
    /// </summary>
    private readonly struct DiscreteCosine16Operator : IHevcInverseTransformOperator<DiscreteCosine16Operator>
    {
        /// <inheritdoc/>
        public static int Size => 16;

        /// <inheritdoc/>
        public static bool UsesButterfly => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCoefficient(int frequency, int position) => GetDiscreteCosineCoefficient(Size, frequency, position);
    }

    /// <summary>
    /// Implements the thirty-two-point inverse discrete cosine transform.
    /// </summary>
    private readonly struct DiscreteCosine32Operator : IHevcInverseTransformOperator<DiscreteCosine32Operator>
    {
        /// <inheritdoc/>
        public static int Size => 32;

        /// <inheritdoc/>
        public static bool UsesButterfly => true;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetCoefficient(int frequency, int position) => GetDiscreteCosineCoefficient(Size, frequency, position);
    }
}
