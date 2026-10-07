// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Normalizes four stored components while preserving their source alpha representation.
/// </summary>
internal readonly struct TiffRgbaOperator : ITiffColorOperator<float>
{
    private readonly float scale;
    private readonly bool associated;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgbaOperator"/> struct.
    /// </summary>
    /// <param name="scale">The reciprocal of the maximum stored integer code.</param>
    /// <param name="associated">Whether stored RGB components have already been multiplied by alpha.</param>
    public TiffRgbaOperator(float scale, bool associated)
    {
        this.scale = scale;
        this.associated = associated;
    }

    /// <inheritdoc />
    public PixelConversionModifiers Modifiers => PixelConversionModifiers.Scale | (this.associated ? PixelConversionModifiers.Premultiply : PixelConversionModifiers.UnPremultiply);

    /// <inheritdoc />
    public void Convert(ref float c0, ref float c1, ref float c2, ref float c3)
    {
        // Associated alpha with a zero code has no representable color contribution.
        if (this.associated && c3 == 0)
        {
            c0 = c1 = c2 = c3 = 0;
            return;
        }

        c0 *= this.scale;
        c1 *= this.scale;
        c2 *= this.scale;
        c3 *= this.scale;
    }

    /// <inheritdoc />
    public void Convert(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
    {
        Vector128<float> factor = Vector128.Create(this.scale);
        c0 *= factor;
        c1 *= factor;
        c2 *= factor;
        c3 *= factor;

        if (this.associated)
        {
            // The equality mask clears only lanes whose stored alpha code was zero.
            Vector128<float> zeroAlpha = Vector128.Equals(c3, Vector128<float>.Zero);
            c0 = Vector128.ConditionalSelect(zeroAlpha, Vector128<float>.Zero, c0);
            c1 = Vector128.ConditionalSelect(zeroAlpha, Vector128<float>.Zero, c1);
            c2 = Vector128.ConditionalSelect(zeroAlpha, Vector128<float>.Zero, c2);
        }
    }

    /// <inheritdoc />
    public void Convert(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
    {
        Vector256<float> factor = Vector256.Create(this.scale);
        c0 *= factor;
        c1 *= factor;
        c2 *= factor;
        c3 *= factor;

        if (this.associated)
        {
            Vector256<float> zeroAlpha = Vector256.Equals(c3, Vector256<float>.Zero);
            c0 = Vector256.ConditionalSelect(zeroAlpha, Vector256<float>.Zero, c0);
            c1 = Vector256.ConditionalSelect(zeroAlpha, Vector256<float>.Zero, c1);
            c2 = Vector256.ConditionalSelect(zeroAlpha, Vector256<float>.Zero, c2);
        }
    }

    /// <inheritdoc />
    public void Convert(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
    {
        Vector512<float> factor = Vector512.Create(this.scale);
        c0 *= factor;
        c1 *= factor;
        c2 *= factor;
        c3 *= factor;

        if (this.associated)
        {
            Vector512<float> zeroAlpha = Vector512.Equals(c3, Vector512<float>.Zero);
            c0 = Vector512.ConditionalSelect(zeroAlpha, Vector512<float>.Zero, c0);
            c1 = Vector512.ConditionalSelect(zeroAlpha, Vector512<float>.Zero, c1);
            c2 = Vector512.ConditionalSelect(zeroAlpha, Vector512<float>.Zero, c2);
        }
    }
}
