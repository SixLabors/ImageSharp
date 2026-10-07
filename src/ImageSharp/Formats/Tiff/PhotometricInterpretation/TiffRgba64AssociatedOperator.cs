// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Clears packed RGBA64 pixels whose associated alpha code is zero.
/// </summary>
internal readonly struct TiffRgba64AssociatedOperator : ITiffColorOperator<ushort>
{
    /// <inheritdoc/>
    public PixelAlphaRepresentation AlphaRepresentation => PixelAlphaRepresentation.Associated;

    /// <inheritdoc/>
    public void Convert(ref ushort c0, ref ushort c1, ref ushort c2, ref ushort c3)
    {
        if (c3 == 0)
        {
            c0 = c1 = c2 = 0;
        }
    }

    /// <inheritdoc/>
    public void Convert(ref Vector128<ushort> c0, ref Vector128<ushort> c1, ref Vector128<ushort> c2, ref Vector128<ushort> c3)
    {
        Vector128<ushort> alphaIndices = Vector128.Create((ushort)3, 3, 3, 3, 7, 7, 7, 7);

        // Each register has two complete RGBA pixels; its alpha stays within its own pixel.
        c0 = Clear(c0, alphaIndices);
        c1 = Clear(c1, alphaIndices);
        c2 = Clear(c2, alphaIndices);
        c3 = Clear(c3, alphaIndices);
    }

    /// <inheritdoc/>
    public void Convert(ref Vector256<ushort> c0, ref Vector256<ushort> c1, ref Vector256<ushort> c2, ref Vector256<ushort> c3)
    {
        Vector256<ushort> alphaIndices = Vector256.Create((ushort)3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15);
        c0 = Clear(c0, alphaIndices);
        c1 = Clear(c1, alphaIndices);
        c2 = Clear(c2, alphaIndices);
        c3 = Clear(c3, alphaIndices);
    }

    /// <inheritdoc/>
    public void Convert(ref Vector512<ushort> c0, ref Vector512<ushort> c1, ref Vector512<ushort> c2, ref Vector512<ushort> c3)
    {
        Vector256<ushort> lower = Vector256.Create((ushort)3, 3, 3, 3, 7, 7, 7, 7, 11, 11, 11, 11, 15, 15, 15, 15);
        Vector256<ushort> upper = Vector256.Create((ushort)19, 19, 19, 19, 23, 23, 23, 23, 27, 27, 27, 27, 31, 31, 31, 31);
        Vector512<ushort> alphaIndices = Vector512.Create(lower, upper);
        c0 = Clear(c0, alphaIndices);
        c1 = Clear(c1, alphaIndices);
        c2 = Clear(c2, alphaIndices);
        c3 = Clear(c3, alphaIndices);
    }

    /// <summary>
    /// Clears complete zero-alpha pixels within one packed register.
    /// </summary>
    /// <param name="pixels">The packed RGBA64 words.</param>
    /// <param name="alphaIndices">The alpha word of each four-word pixel, repeated across its lanes.</param>
    /// <returns>The pixels after hidden-color clearing.</returns>
    private static Vector128<ushort> Clear(Vector128<ushort> pixels, Vector128<ushort> alphaIndices)
    {
        Vector128<ushort> alpha = Vector128.Shuffle(pixels, alphaIndices);
        Vector128<ushort> zeroAlpha = Vector128.Equals(alpha, Vector128<ushort>.Zero);
        return Vector128.ConditionalSelect(zeroAlpha, Vector128<ushort>.Zero, pixels);
    }

    /// <inheritdoc cref="Clear(Vector128{ushort}, Vector128{ushort})"/>
    private static Vector256<ushort> Clear(Vector256<ushort> pixels, Vector256<ushort> alphaIndices)
    {
        Vector256<ushort> alpha = Vector256.Shuffle(pixels, alphaIndices);
        Vector256<ushort> zeroAlpha = Vector256.Equals(alpha, Vector256<ushort>.Zero);
        return Vector256.ConditionalSelect(zeroAlpha, Vector256<ushort>.Zero, pixels);
    }

    /// <inheritdoc cref="Clear(Vector128{ushort}, Vector128{ushort})"/>
    private static Vector512<ushort> Clear(Vector512<ushort> pixels, Vector512<ushort> alphaIndices)
    {
        Vector512<ushort> alpha = Vector512.Shuffle(pixels, alphaIndices);
        Vector512<ushort> zeroAlpha = Vector512.Equals(alpha, Vector512<ushort>.Zero);
        return Vector512.ConditionalSelect(zeroAlpha, Vector512<ushort>.Zero, pixels);
    }
}
