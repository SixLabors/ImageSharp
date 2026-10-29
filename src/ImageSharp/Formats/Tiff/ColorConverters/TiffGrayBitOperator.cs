// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Normalizes bit-packed grayscale codes and expands them to opaque RGB.
/// </summary>
internal readonly struct TiffGrayBitOperator : ITiffColorOperator<float>
{
    private readonly ushort bitsPerSample;
    private readonly float maximumCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffGrayBitOperator"/> struct.
    /// </summary>
    /// <param name="bitsPerSample">The stored grayscale bit depth.</param>
    /// <param name="maximumCode">The greatest encoded grayscale code.</param>
    public TiffGrayBitOperator(ushort bitsPerSample, float maximumCode)
    {
        this.bitsPerSample = bitsPerSample;
        this.maximumCode = maximumCode;
    }

    /// <inheritdoc />
    public PixelAlphaRepresentation AlphaRepresentation => PixelAlphaRepresentation.None;

    /// <inheritdoc />
    public void Convert(ref float c0, ref float c1, ref float c2, ref float c3)
    {
        if (this.bitsPerSample == 4)
        {
            // Existing four-bit decoding first expands the nibble to an L8 byte on its exact 17-step lattice.
            c0 = (c0 * 17F) / 255F;
        }
        else if (this.bitsPerSample != 1)
        {
            c0 /= this.maximumCode;
        }

        c1 = c2 = c0;
        c3 = 1F;
    }

    /// <inheritdoc />
    public void Convert(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
    {
        if (this.bitsPerSample == 4)
        {
            c0 = (c0 * Vector128.Create(17F)) / Vector128.Create(255F);
        }
        else if (this.bitsPerSample != 1)
        {
            c0 /= Vector128.Create(this.maximumCode);
        }

        c1 = c2 = c0;
        c3 = Vector128.Create(1F);
    }

    /// <inheritdoc />
    public void Convert(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
    {
        if (this.bitsPerSample == 4)
        {
            c0 = (c0 * Vector256.Create(17F)) / Vector256.Create(255F);
        }
        else if (this.bitsPerSample != 1)
        {
            c0 /= Vector256.Create(this.maximumCode);
        }

        c1 = c2 = c0;
        c3 = Vector256.Create(1F);
    }

    /// <inheritdoc />
    public void Convert(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
    {
        if (this.bitsPerSample == 4)
        {
            c0 = (c0 * Vector512.Create(17F)) / Vector512.Create(255F);
        }
        else if (this.bitsPerSample != 1)
        {
            c0 /= Vector512.Create(this.maximumCode);
        }

        c1 = c2 = c0;
        c3 = Vector512.Create(1F);
    }
}
