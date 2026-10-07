// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Normalizes stored RGB codes and adds opaque alpha.
/// </summary>
internal readonly struct TiffRgbOperator : ITiffColorOperator<float>
{
    private readonly float scale;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgbOperator"/> struct.
    /// </summary>
    /// <param name="scale">The reciprocal of the maximum stored integer code.</param>
    public TiffRgbOperator(float scale) => this.scale = scale;

    /// <inheritdoc />
    public PixelConversionModifiers Modifiers => PixelConversionModifiers.Scale;

    /// <inheritdoc />
    public void Convert(ref float c0, ref float c1, ref float c2, ref float c3)
    {
        c0 *= this.scale;
        c1 *= this.scale;
        c2 *= this.scale;
        c3 = 1F;
    }

    /// <inheritdoc />
    public void Convert(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
    {
        Vector128<float> factor = Vector128.Create(this.scale);
        c0 *= factor;
        c1 *= factor;
        c2 *= factor;
        c3 = Vector128.Create(1F);
    }

    /// <inheritdoc />
    public void Convert(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
    {
        Vector256<float> factor = Vector256.Create(this.scale);
        c0 *= factor;
        c1 *= factor;
        c2 *= factor;
        c3 = Vector256.Create(1F);
    }

    /// <inheritdoc />
    public void Convert(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
    {
        Vector512<float> factor = Vector512.Create(this.scale);
        c0 *= factor;
        c1 *= factor;
        c2 *= factor;
        c3 = Vector512.Create(1F);
    }
}
