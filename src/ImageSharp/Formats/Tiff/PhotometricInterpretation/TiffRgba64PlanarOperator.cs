// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Clears hidden planar RGB codes where an associated RGBA64 alpha code is zero.
/// </summary>
internal readonly struct TiffRgba64PlanarOperator : ITiffColorOperator<ushort>
{
    /// <inheritdoc/>
    public PixelConversionModifiers Modifiers => PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply;

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
        // Planar lanes align by pixel, so one alpha mask clears the three matching RGB lanes.
        Vector128<ushort> zeroAlpha = Vector128.Equals(c3, Vector128<ushort>.Zero);
        c0 = Vector128.ConditionalSelect(zeroAlpha, Vector128<ushort>.Zero, c0);
        c1 = Vector128.ConditionalSelect(zeroAlpha, Vector128<ushort>.Zero, c1);
        c2 = Vector128.ConditionalSelect(zeroAlpha, Vector128<ushort>.Zero, c2);
    }

    /// <inheritdoc/>
    public void Convert(ref Vector256<ushort> c0, ref Vector256<ushort> c1, ref Vector256<ushort> c2, ref Vector256<ushort> c3)
    {
        Vector256<ushort> zeroAlpha = Vector256.Equals(c3, Vector256<ushort>.Zero);
        c0 = Vector256.ConditionalSelect(zeroAlpha, Vector256<ushort>.Zero, c0);
        c1 = Vector256.ConditionalSelect(zeroAlpha, Vector256<ushort>.Zero, c1);
        c2 = Vector256.ConditionalSelect(zeroAlpha, Vector256<ushort>.Zero, c2);
    }

    /// <inheritdoc/>
    public void Convert(ref Vector512<ushort> c0, ref Vector512<ushort> c1, ref Vector512<ushort> c2, ref Vector512<ushort> c3)
    {
        Vector512<ushort> zeroAlpha = Vector512.Equals(c3, Vector512<ushort>.Zero);
        c0 = Vector512.ConditionalSelect(zeroAlpha, Vector512<ushort>.Zero, c0);
        c1 = Vector512.ConditionalSelect(zeroAlpha, Vector512<ushort>.Zero, c1);
        c2 = Vector512.ConditionalSelect(zeroAlpha, Vector512<ushort>.Zero, c2);
    }
}
