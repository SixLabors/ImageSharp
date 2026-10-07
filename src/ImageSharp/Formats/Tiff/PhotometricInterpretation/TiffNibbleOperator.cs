// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Normalizes four-bit RGB and RGBA codes while retaining the stored alpha interpretation.
/// </summary>
internal readonly struct TiffNibbleOperator : ITiffColorOperator<float>
{
    private readonly bool hasAlpha;
    private readonly bool associated;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffNibbleOperator"/> struct.
    /// </summary>
    /// <param name="hasAlpha">Whether the stored pixels contain an alpha nibble.</param>
    /// <param name="associated">Whether stored RGB components are multiplied by alpha.</param>
    public TiffNibbleOperator(bool hasAlpha, bool associated)
    {
        this.hasAlpha = hasAlpha;
        this.associated = associated;
    }

    /// <inheritdoc />
    public PixelConversionModifiers Modifiers => PixelConversionModifiers.Scale | (this.hasAlpha ? this.associated ? PixelConversionModifiers.Premultiply : PixelConversionModifiers.UnPremultiply : PixelConversionModifiers.None);

    /// <inheritdoc />
    public void Convert(ref float c0, ref float c1, ref float c2, ref float c3)
    {
        // Division matches the existing bit-packed color operator at floating-point targets.
        c0 /= 15F;
        c1 /= 15F;
        c2 /= 15F;
        c3 = this.hasAlpha ? c3 / 15F : 1F;
    }

    /// <inheritdoc />
    public void Convert(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
    {
        Vector128<float> divisor = Vector128.Create(15F);
        c0 /= divisor;
        c1 /= divisor;
        c2 /= divisor;
        c3 = this.hasAlpha ? c3 / divisor : Vector128.Create(1F);
    }

    /// <inheritdoc />
    public void Convert(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
    {
        Vector256<float> divisor = Vector256.Create(15F);
        c0 /= divisor;
        c1 /= divisor;
        c2 /= divisor;
        c3 = this.hasAlpha ? c3 / divisor : Vector256.Create(1F);
    }

    /// <inheritdoc />
    public void Convert(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
    {
        Vector512<float> divisor = Vector512.Create(15F);
        c0 /= divisor;
        c1 /= divisor;
        c2 /= divisor;
        c3 = this.hasAlpha ? c3 / divisor : Vector512.Create(1F);
    }
}
