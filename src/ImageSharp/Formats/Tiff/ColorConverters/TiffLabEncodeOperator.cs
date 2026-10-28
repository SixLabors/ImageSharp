// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Maps Lab values to TIFF lightness and two's-complement chroma codes before integer packing.
/// </summary>
internal readonly struct TiffLabEncodeOperator : ITiffColorOperator<float>
{
    private readonly float chromaScale;
    private readonly float chromaMinimum;
    private readonly float chromaMaximum;
    private readonly int codeMask;
    private readonly float codeScale;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffLabEncodeOperator"/> struct.
    /// </summary>
    /// <param name="sixteenBit">Whether each stored sample occupies sixteen bits.</param>
    public TiffLabEncodeOperator(bool sixteenBit)
    {
        // Eight-bit chroma stores whole Lab units. Sixteen-bit chroma stores 256 codes per Lab unit.
        this.chromaScale = sixteenBit ? 256F : 1F;
        this.chromaMinimum = sixteenBit ? short.MinValue : sbyte.MinValue;
        this.chromaMaximum = sixteenBit ? short.MaxValue : sbyte.MaxValue;
        this.codeMask = sixteenBit ? ushort.MaxValue : byte.MaxValue;
        this.codeScale = 1F / this.codeMask;
    }

    /// <inheritdoc />
    public PixelAlphaRepresentation AlphaRepresentation => PixelAlphaRepresentation.None;

    /// <inheritdoc />
    public void Convert(ref float c0, ref float c1, ref float c2, ref float c3)
    {
        // Lightness maps 0..100 to the full unsigned range.
        c0 *= 1F / 100F;

        // Round chroma in signed sample units, then keep the low eight or sixteen two's-complement bits.
        // The shared integer packer expects normalized unsigned values, so apply the reciprocal unsigned maximum last.
        c1 = ((int)MathF.Round(ColorNumerics.ClampToFinite(c1 * this.chromaScale, this.chromaMinimum, this.chromaMaximum)) & this.codeMask)
            * this.codeScale;

        c2 = ((int)MathF.Round(ColorNumerics.ClampToFinite(c2 * this.chromaScale, this.chromaMinimum, this.chromaMaximum)) & this.codeMask)
            * this.codeScale;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Convert(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
    {
        // Each lane holds one pixel's component. Match scalar clipping, signed rounding and bit preservation.
        Vector128<float> scale = Vector128.Create(this.chromaScale);
        Vector128<float> minimum = Vector128.Create(this.chromaMinimum);
        Vector128<float> maximum = Vector128.Create(this.chromaMaximum);
        Vector128<int> mask = Vector128.Create(this.codeMask);
        Vector128<float> codeScale = Vector128.Create(this.codeScale);

        c0 *= Vector128.Create(1F / 100F);

        // Round signed chroma before masking. A negative code keeps its stored bits instead of being clipped to zero.
        Vector128<int> a = Vector128_.ConvertToInt32RoundToEven(ColorNumerics.ClampToFinite(c1 * scale, minimum, maximum));
        Vector128<int> b = Vector128_.ConvertToInt32RoundToEven(ColorNumerics.ClampToFinite(c2 * scale, minimum, maximum));

        // Normalize the unsigned codes for the same final packer used by the scalar path.
        c1 = Vector128.ConvertToSingle(a & mask) * codeScale;
        c2 = Vector128.ConvertToSingle(b & mask) * codeScale;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Convert(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
    {
        // Each lane holds one pixel's component. Match scalar clipping, signed rounding and bit preservation.
        Vector256<float> scale = Vector256.Create(this.chromaScale);
        Vector256<float> minimum = Vector256.Create(this.chromaMinimum);
        Vector256<float> maximum = Vector256.Create(this.chromaMaximum);
        Vector256<int> mask = Vector256.Create(this.codeMask);
        Vector256<float> codeScale = Vector256.Create(this.codeScale);

        c0 *= Vector256.Create(1F / 100F);

        // Round signed chroma before masking. A negative code keeps its stored bits instead of being clipped to zero.
        Vector256<int> a = Vector256_.ConvertToInt32RoundToEven(ColorNumerics.ClampToFinite(c1 * scale, minimum, maximum));
        Vector256<int> b = Vector256_.ConvertToInt32RoundToEven(ColorNumerics.ClampToFinite(c2 * scale, minimum, maximum));

        // Normalize the unsigned codes for the same final packer used by the scalar path.
        c1 = Vector256.ConvertToSingle(a & mask) * codeScale;
        c2 = Vector256.ConvertToSingle(b & mask) * codeScale;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Convert(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
    {
        // Each lane holds one pixel's component. Match scalar clipping, signed rounding and bit preservation.
        Vector512<float> scale = Vector512.Create(this.chromaScale);
        Vector512<float> minimum = Vector512.Create(this.chromaMinimum);
        Vector512<float> maximum = Vector512.Create(this.chromaMaximum);
        Vector512<int> mask = Vector512.Create(this.codeMask);
        Vector512<float> codeScale = Vector512.Create(this.codeScale);

        c0 *= Vector512.Create(1F / 100F);

        // Round signed chroma before masking. A negative code keeps its stored bits instead of being clipped to zero.
        Vector512<int> a = Vector512_.ConvertToInt32RoundToEven(ColorNumerics.ClampToFinite(c1 * scale, minimum, maximum));
        Vector512<int> b = Vector512_.ConvertToInt32RoundToEven(ColorNumerics.ClampToFinite(c2 * scale, minimum, maximum));

        // Normalize the unsigned codes for the same final packer used by the scalar path.
        c1 = Vector512.ConvertToSingle(a & mask) * codeScale;
        c2 = Vector512.ConvertToSingle(b & mask) * codeScale;
    }
}
