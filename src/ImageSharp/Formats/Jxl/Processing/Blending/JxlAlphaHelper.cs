// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

/// <summary>
/// Applies JPEG XL's native alpha representation using its nonzero divisor floor.
/// </summary>
internal static class JxlAlphaHelper
{
    // JPEG XL retains color at zero alpha by using this nonzero factor for both directions.
    // Use the same floor as libjxl alpha.h so association can be reversed without losing those samples.
    private const float SmallAlpha = 1F / (1 << 26);

    /// <summary>
    /// Associates planar RGB samples using JPEG XL's nonzero alpha floor.
    /// </summary>
    /// <param name="red">The red samples, modified in place.</param>
    /// <param name="green">The green samples, modified in place.</param>
    /// <param name="blue">The blue samples, modified in place.</param>
    /// <param name="alpha">The unchanged alpha samples.</param>
    /// <param name="pixelCount">The number of samples per channel.</param>
    public static void PremultiplyAlpha(Span<float> red, Span<float> green, Span<float> blue, ReadOnlySpan<float> alpha, int pixelCount)
    {
        // Reuse one register-sized block for the three channels. This bounds stack storage and avoids a full alpha-row copy.
        InlineArray16<float> storage = default;
        Span<float> factors = storage;

        for (int offset = 0; offset < pixelCount; offset += factors.Length)
        {
            int count = Math.Min(factors.Length, pixelCount - offset);
            Span<float> block = factors[..count];
            // Native std::max places the finite floor first, so NaN selects that floor rather than propagating.
            TensorPrimitives.MaxNumber(alpha.Slice(offset, count), SmallAlpha, block);

            TensorPrimitives.Multiply(red.Slice(offset, count), block, red.Slice(offset, count));
            TensorPrimitives.Multiply(green.Slice(offset, count), block, green.Slice(offset, count));
            TensorPrimitives.Multiply(blue.Slice(offset, count), block, blue.Slice(offset, count));
        }
    }

    /// <summary>
    /// Restores unassociated planar RGB samples using JPEG XL's nonzero alpha floor.
    /// </summary>
    /// <param name="red">The red samples, modified in place.</param>
    /// <param name="green">The green samples, modified in place.</param>
    /// <param name="blue">The blue samples, modified in place.</param>
    /// <param name="alpha">The unchanged alpha samples.</param>
    /// <param name="pixelCount">The number of samples per channel.</param>
    public static void UnpremultiplyAlpha(Span<float> red, Span<float> green, Span<float> blue, ReadOnlySpan<float> alpha, int pixelCount)
    {
        // Reuse one register-sized block for the three channels. This bounds stack storage and avoids a full alpha-row copy.
        InlineArray16<float> storage = default;
        Span<float> factors = storage;

        for (int offset = 0; offset < pixelCount; offset += factors.Length)
        {
            int count = Math.Min(factors.Length, pixelCount - offset);
            Span<float> block = factors[..count];
            // Use the same finite floor for both directions, including native NaN handling.
            TensorPrimitives.MaxNumber(alpha.Slice(offset, count), SmallAlpha, block);
            TensorPrimitives.Divide(1F, block, block);

            TensorPrimitives.Multiply(red.Slice(offset, count), block, red.Slice(offset, count));
            TensorPrimitives.Multiply(green.Slice(offset, count), block, green.Slice(offset, count));
            TensorPrimitives.Multiply(blue.Slice(offset, count), block, blue.Slice(offset, count));
        }
    }
}
