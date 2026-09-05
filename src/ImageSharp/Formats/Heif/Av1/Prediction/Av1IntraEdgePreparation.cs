// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Prepares directional intra-reference edges for AV1 smoothing and half-sample prediction.
/// </summary>
internal static class Av1IntraEdgePreparation
{
    /// <summary>
    /// The number of samples reserved before the first edge sample.
    /// </summary>
    public const int ReferencePrefixLength = 16;

    /// <summary>
    /// The total sample capacity of one edge including prefix and extension.
    /// </summary>
    public const int ReferenceBufferLength = (2 * Av1Constants.MaxTransformSize) + 32;

    /// <summary>
    /// Filters and upsamples prepared directional reference edges.
    /// </summary>
    /// <typeparam name="T">The byte or signed high-bit-depth sample type.</typeparam>
    /// <param name="above">The top edge with writable prefix and extension.</param>
    /// <param name="left">The left edge with writable prefix and extension.</param>
    /// <param name="width">The transform width.</param>
    /// <param name="height">The transform height.</param>
    /// <param name="angle">The adjusted directional angle.</param>
    /// <param name="topCount">The number of available top samples before extension.</param>
    /// <param name="leftCount">The number of available left samples before extension.</param>
    /// <param name="filterType">Whether a relevant neighbor uses smooth prediction.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="scratch">The original-edge workspace with at least <see cref="Av1IntraEdgeFilter.ScratchLength"/> samples.</param>
    /// <param name="upsampleAbove">Whether the top edge contains half-sample positions.</param>
    /// <param name="upsampleLeft">Whether the left edge contains half-sample positions.</param>
    public static void Prepare<T>(
        Span<T> above,
        Span<T> left,
        int width,
        int height,
        int angle,
        int topCount,
        int leftCount,
        bool filterType,
        int bitDepth,
        Span<T> scratch,
        out bool upsampleAbove,
        out bool upsampleLeft)
        where T : unmanaged, IBinaryInteger<T>
    {
        bool needAbove = angle < 180;
        bool needLeft = angle > 90;
        bool needRight = angle < 90;
        bool needBottom = angle > 180;
        upsampleAbove = false;
        upsampleLeft = false;

        // A missing sole edge produces a constant block from the perpendicular sample or midpoint offset.
        // Its prepared edge already repeats that value. Upsampling its distinct corner would change it.
        if ((!needAbove && leftCount == 0) || (!needLeft && topCount == 0))
        {
            return;
        }

        if (angle is not 90 and not 180)
        {
            if (needAbove && needLeft && width + height >= 24)
            {
                // The corner is one logical sample represented in both edge prefixes. Filter it first,
                // then let both edge convolutions read the same rounded [5, 6, 5] corner value.
                ref T corner = ref Unsafe.Subtract(ref above[0], 1);
                int value = (5 * int.CreateChecked(left[0]))
                    + (6 * int.CreateChecked(corner))
                    + (5 * int.CreateChecked(above[0]));

                corner = T.CreateChecked((value + 8) >> 4);
                Unsafe.Subtract(ref left[0], 1) = corner;
            }

            if (needAbove && topCount > 0)
            {
                int strength = IntraEdgeFilterStrength(width, height, angle - 90, filterType);
                Filter(ref Unsafe.Subtract(ref above[0], 1), topCount + 1 + (needRight ? height : 0), strength, scratch);
            }

            if (needLeft && leftCount > 0)
            {
                int strength = IntraEdgeFilterStrength(height, width, angle - 180, filterType);
                Filter(ref Unsafe.Subtract(ref left[0], 1), leftCount + 1 + (needBottom ? width : 0), strength, scratch);
            }
        }

        upsampleAbove = UseUpsampling(width, height, angle - 90, filterType);
        if (needAbove && upsampleAbove)
        {
            Upsample(above, width + (needRight ? height : 0), bitDepth, scratch);
        }

        upsampleLeft = UseUpsampling(height, width, angle - 180, filterType);
        if (needLeft && upsampleLeft)
        {
            Upsample(left, height + (needBottom ? width : 0), bitDepth, scratch);
        }
    }

    /// <summary>
    /// Selects half-sample interpolation for a transform edge.
    /// </summary>
    /// <param name="width">The transform width.</param>
    /// <param name="height">The transform height.</param>
    /// <param name="delta">The angle relative to the edge's cardinal direction.</param>
    /// <param name="filterType">Whether a relevant neighbor uses smooth prediction.</param>
    /// <returns>Whether the edge uses half-sample interpolation.</returns>
    private static bool UseUpsampling(int width, int height, int delta, bool filterType)
    {
        int distance = Math.Abs(delta);
        return distance > 0 && distance < 40 && width + height <= (filterType ? 8 : 16);
    }

    /// <summary>
    /// Dispatches edge smoothing to the concrete sample representation.
    /// </summary>
    /// <typeparam name="T">The byte or signed high-bit-depth sample type.</typeparam>
    /// <param name="edge">The first edge sample, including the corner.</param>
    /// <param name="count">The number of edge samples.</param>
    /// <param name="strength">The smoothing strength.</param>
    /// <param name="scratch">The reusable original-edge workspace.</param>
    private static void Filter<T>(ref T edge, int count, int strength, Span<T> scratch)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
        {
            Av1IntraEdgeFilter.Apply(ref Unsafe.As<T, byte>(ref edge), count, strength, MemoryMarshal.Cast<T, byte>(scratch));
        }
        else
        {
            Av1IntraEdgeFilter.Apply(ref Unsafe.As<T, short>(ref edge), count, strength, MemoryMarshal.Cast<T, short>(scratch));
        }
    }

    /// <summary>
    /// Dispatches half-sample interpolation to the concrete sample representation.
    /// </summary>
    /// <typeparam name="T">The byte or signed high-bit-depth sample type.</typeparam>
    /// <param name="edge">The edge with writable prefix and extension.</param>
    /// <param name="count">The number of original edge samples.</param>
    /// <param name="bitDepth">The coded precision.</param>
    /// <param name="scratch">The reusable original-edge workspace.</param>
    private static void Upsample<T>(Span<T> edge, int count, int bitDepth, Span<T> scratch)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
        {
            Av1IntraEdgeUpsampler.Apply(MemoryMarshal.Cast<T, byte>(edge), count, MemoryMarshal.Cast<T, byte>(scratch));
        }
        else
        {
            Av1IntraEdgeUpsampler.Apply(MemoryMarshal.Cast<T, short>(edge), count, bitDepth, MemoryMarshal.Cast<T, short>(scratch));
        }
    }

    /// <summary>
    /// Selects the AV1 intra-edge filter strength for the block dimensions and prediction angle.
    /// </summary>
    /// <param name="width">The edge's primary block dimension.</param>
    /// <param name="height">The edge's secondary block dimension.</param>
    /// <param name="delta">The prediction angle relative to the edge's cardinal direction.</param>
    /// <param name="filterType">A value indicating whether a neighboring smooth mode selects the alternate thresholds.</param>
    /// <returns>The filter strength from zero for no filtering through three for the strongest kernel.</returns>
    private static int IntraEdgeFilterStrength(int width, int height, int delta, bool filterType)
    {
        int d = Math.Abs(delta);
        int strength = 0;
        int widthHeight = width + height;
        if (!filterType)
        {
            if (widthHeight <= 8)
            {
                if (d >= 56)
                {
                    strength = 1;
                }
            }
            else if (widthHeight <= 12)
            {
                if (d >= 40)
                {
                    strength = 1;
                }
            }
            else if (widthHeight <= 16)
            {
                if (d >= 40)
                {
                    strength = 1;
                }
            }
            else if (widthHeight <= 24)
            {
                if (d >= 8)
                {
                    strength = 1;
                }

                if (d >= 16)
                {
                    strength = 2;
                }

                if (d >= 32)
                {
                    strength = 3;
                }
            }
            else if (widthHeight <= 32)
            {
                if (d >= 1)
                {
                    strength = 1;
                }

                if (d >= 4)
                {
                    strength = 2;
                }

                if (d >= 32)
                {
                    strength = 3;
                }
            }
            else
            {
                if (d >= 1)
                {
                    strength = 3;
                }
            }
        }
        else
        {
            if (widthHeight <= 8)
            {
                if (d >= 40)
                {
                    strength = 1;
                }

                if (d >= 64)
                {
                    strength = 2;
                }
            }
            else if (widthHeight <= 16)
            {
                if (d >= 20)
                {
                    strength = 1;
                }

                if (d >= 48)
                {
                    strength = 2;
                }
            }
            else if (widthHeight <= 24)
            {
                if (d >= 4)
                {
                    strength = 3;
                }
            }
            else
            {
                if (d >= 1)
                {
                    strength = 3;
                }
            }
        }

        return strength;
    }
}
