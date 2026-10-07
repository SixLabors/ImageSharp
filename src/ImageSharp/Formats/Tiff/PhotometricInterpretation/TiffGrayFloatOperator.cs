// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Expands floating-point TIFF grayscale samples into opaque RGB values.
/// </summary>
internal readonly struct TiffGrayFloatOperator : ITiffColorOperator<float>
{
    private readonly bool whiteIsZero;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffGrayFloatOperator"/> struct.
    /// </summary>
    /// <param name="whiteIsZero">Whether stored intensity is the complement of decoded intensity.</param>
    public TiffGrayFloatOperator(bool whiteIsZero) => this.whiteIsZero = whiteIsZero;

    /// <inheritdoc />
    public PixelConversionModifiers Modifiers => PixelConversionModifiers.Scale;

    /// <inheritdoc />
    public void Convert(ref float c0, ref float c1, ref float c2, ref float c3)
    {
        // BlackIsZero preserves the stored float bit pattern, including signed zero and NaN payloads.
        if (this.whiteIsZero)
        {
            c0 = 1F - c0;
        }

        c1 = c2 = c0;
        c3 = 1F;
    }

    /// <inheritdoc />
    public void Convert(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
    {
        if (this.whiteIsZero)
        {
            c0 = Vector128.Create(1F) - c0;
        }

        c1 = c2 = c0;
        c3 = Vector128.Create(1F);
    }

    /// <inheritdoc />
    public void Convert(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
    {
        if (this.whiteIsZero)
        {
            c0 = Vector256.Create(1F) - c0;
        }

        c1 = c2 = c0;
        c3 = Vector256.Create(1F);
    }

    /// <inheritdoc />
    public void Convert(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
    {
        if (this.whiteIsZero)
        {
            c0 = Vector512.Create(1F) - c0;
        }

        c1 = c2 = c0;
        c3 = Vector512.Create(1F);
    }
}
