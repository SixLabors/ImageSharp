// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Passes floating-point RGBA samples to destination pixel conversion unchanged.
/// </summary>
internal readonly struct TiffFloatRgbaOperator : ITiffColorOperator<float>
{
    private readonly bool associated;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffFloatRgbaOperator"/> struct.
    /// </summary>
    /// <param name="associated">Whether stored RGB components have already been multiplied by alpha.</param>
    public TiffFloatRgbaOperator(bool associated) => this.associated = associated;

    /// <inheritdoc />
    public PixelAlphaRepresentation AlphaRepresentation => this.associated ? PixelAlphaRepresentation.Associated : PixelAlphaRepresentation.Unassociated;

    /// <inheritdoc />
    public void Convert(ref float c0, ref float c1, ref float c2, ref float c3)
    {
    }

    /// <inheritdoc />
    public void Convert(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
    {
    }

    /// <inheritdoc />
    public void Convert(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
    {
    }

    /// <inheritdoc />
    public void Convert(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
    {
    }
}
