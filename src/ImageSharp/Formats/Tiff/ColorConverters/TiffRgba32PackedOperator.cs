// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Normalizes packed RGBA32 code groups and clears zero-alpha associated color.
/// </summary>
internal readonly struct TiffRgba32PackedOperator : ITiffColorOperator<float>
{
    private const float Scale = 1F / uint.MaxValue;
    private readonly bool associated;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgba32PackedOperator"/> struct.
    /// </summary>
    /// <param name="associated">Whether stored RGB codes have already been multiplied by alpha.</param>
    public TiffRgba32PackedOperator(bool associated) => this.associated = associated;

    /// <inheritdoc/>
    public PixelAlphaRepresentation AlphaRepresentation => this.associated ? PixelAlphaRepresentation.Associated : PixelAlphaRepresentation.Unassociated;

    /// <inheritdoc/>
    public void Convert(ref float c0, ref float c1, ref float c2, ref float c3)
    {
        if (this.associated && c3 == 0)
        {
            c0 = c1 = c2 = 0;
        }

        c0 *= Scale;
        c1 *= Scale;
        c2 *= Scale;
        c3 *= Scale;
    }

    /// <inheritdoc/>
    public void Convert(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
    {
        Vector128<uint> alphaIndices = Vector128.Create(3U);
        c0 = this.ConvertPacked(c0, alphaIndices);
        c1 = this.ConvertPacked(c1, alphaIndices);
        c2 = this.ConvertPacked(c2, alphaIndices);
        c3 = this.ConvertPacked(c3, alphaIndices);
    }

    /// <inheritdoc/>
    public void Convert(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
    {
        Vector256<uint> alphaIndices = Vector256.Create(3U, 3U, 3U, 3U, 7U, 7U, 7U, 7U);
        c0 = this.ConvertPacked(c0, alphaIndices);
        c1 = this.ConvertPacked(c1, alphaIndices);
        c2 = this.ConvertPacked(c2, alphaIndices);
        c3 = this.ConvertPacked(c3, alphaIndices);
    }

    /// <inheritdoc/>
    public void Convert(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
    {
        Vector512<uint> alphaIndices = Vector512.Create(3U, 3U, 3U, 3U, 7U, 7U, 7U, 7U, 11U, 11U, 11U, 11U, 15U, 15U, 15U, 15U);
        c0 = this.ConvertPacked(c0, alphaIndices);
        c1 = this.ConvertPacked(c1, alphaIndices);
        c2 = this.ConvertPacked(c2, alphaIndices);
        c3 = this.ConvertPacked(c3, alphaIndices);
    }

    /// <summary>
    /// Normalizes one complete 128-bit packed pixel group.
    /// </summary>
    /// <param name="codes">One RGBA code group.</param>
    /// <param name="alphaIndices">The alpha lane replicated across the pixel.</param>
    /// <returns>The scaled pixel group.</returns>
    private Vector128<float> ConvertPacked(Vector128<float> codes, Vector128<uint> alphaIndices)
    {
        Vector128<float> scaled = codes * Vector128.Create(Scale);

        if (this.associated)
        {
            // Every index selects its pixel's alpha within this register, so the native shuffle needs no range checks.
            Vector128<float> alpha = Vector128.ShuffleNative(codes.AsUInt32(), alphaIndices).AsSingle();
            scaled = Vector128.ConditionalSelect(Vector128.Equals(alpha, Vector128<float>.Zero), Vector128<float>.Zero, scaled);
        }

        return scaled;
    }

    /// <inheritdoc cref="ConvertPacked(Vector128{float}, Vector128{uint})"/>
    private Vector256<float> ConvertPacked(Vector256<float> codes, Vector256<uint> alphaIndices)
    {
        Vector256<float> scaled = codes * Vector256.Create(Scale);

        if (this.associated)
        {
            // Every index selects its pixel's alpha within this register, so the native shuffle needs no range checks.
            Vector256<float> alpha = Vector256.ShuffleNative(codes.AsUInt32(), alphaIndices).AsSingle();
            scaled = Vector256.ConditionalSelect(Vector256.Equals(alpha, Vector256<float>.Zero), Vector256<float>.Zero, scaled);
        }

        return scaled;
    }

    /// <inheritdoc cref="ConvertPacked(Vector128{float}, Vector128{uint})"/>
    private Vector512<float> ConvertPacked(Vector512<float> codes, Vector512<uint> alphaIndices)
    {
        Vector512<float> scaled = codes * Vector512.Create(Scale);

        if (this.associated)
        {
            // Every index selects its pixel's alpha within this register, so the native shuffle needs no range checks.
            Vector512<float> alpha = Vector512.ShuffleNative(codes.AsUInt32(), alphaIndices).AsSingle();
            scaled = Vector512.ConditionalSelect(Vector512.Equals(alpha, Vector512<float>.Zero), Vector512<float>.Zero, scaled);
        }

        return scaled;
    }
}
