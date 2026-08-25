// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the effective HEVC quantization parameters for one transform unit.
/// </summary>
internal readonly struct HevcQuantizationParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HevcQuantizationParameters"/> struct.
    /// </summary>
    /// <param name="lumaQuantizationParameter">The effective coding-unit luma quantization parameter before the luma bit-depth offset.</param>
    /// <param name="lumaBitDepth">The reconstructed luma precision.</param>
    /// <param name="chromaBitDepth">The reconstructed chroma precision.</param>
    /// <param name="chromaFormat">The sequence chroma-format identifier.</param>
    /// <param name="cbQuantizationParameterOffset">The combined picture, slice, and coding-unit Cb quantization-parameter offset.</param>
    /// <param name="crQuantizationParameterOffset">The combined picture, slice, and coding-unit Cr quantization-parameter offset.</param>
    public HevcQuantizationParameters(
        int lumaQuantizationParameter,
        int lumaBitDepth,
        int chromaBitDepth,
        byte chromaFormat,
        int cbQuantizationParameterOffset,
        int crQuantizationParameterOffset)
    {
        int lumaBitDepthOffset = 6 * (lumaBitDepth - 8);
        int chromaBitDepthOffset = 6 * (chromaBitDepth - 8);
        this.CbOffset = cbQuantizationParameterOffset;
        this.CrOffset = crQuantizationParameterOffset;
        this.Luma = lumaQuantizationParameter + lumaBitDepthOffset;
        this.Cb = GetChromaQuantizationParameter(lumaQuantizationParameter, cbQuantizationParameterOffset, chromaBitDepthOffset, chromaFormat);
        this.Cr = GetChromaQuantizationParameter(lumaQuantizationParameter, crQuantizationParameterOffset, chromaBitDepthOffset, chromaFormat);
    }

    /// <summary>
    /// Gets the effective nonnegative luma quantization parameter including its bit-depth offset.
    /// </summary>
    public int Luma { get; }

    /// <summary>
    /// Gets the effective nonnegative blue-difference chroma quantization parameter including its bit-depth offset.
    /// </summary>
    public int Cb { get; }

    /// <summary>
    /// Gets the effective nonnegative red-difference chroma quantization parameter including its bit-depth offset.
    /// </summary>
    public int Cr { get; }

    /// <summary>
    /// Gets the combined picture, slice, and coding-unit Cb quantization-parameter offset.
    /// </summary>
    public int CbOffset { get; }

    /// <summary>
    /// Gets the combined picture, slice, and coding-unit Cr quantization-parameter offset.
    /// </summary>
    public int CrOffset { get; }

    /// <summary>
    /// Gets the H.265 Table 8-10 chroma quantization-parameter mapping for 4:2:0 pictures.
    /// </summary>
    private static ReadOnlySpan<byte> Chroma420QuantizationParameterMap =>
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
        29, 30, 31, 32, 33, 33, 34, 34, 35, 35, 36, 36, 37, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51,
    ];

    /// <summary>
    /// Gets the effective quantization parameter for the selected reconstruction plane.
    /// </summary>
    /// <param name="plane">The reconstruction plane.</param>
    /// <returns>The effective nonnegative quantization parameter including its bit-depth offset.</returns>
    public int Get(HevcPlane plane) => plane switch
    {
        HevcPlane.Y => this.Luma,
        HevcPlane.Cb => this.Cb,
        _ => this.Cr,
    };

    /// <summary>
    /// Derives an effective chroma quantization parameter from the luma value and combined component offset.
    /// </summary>
    /// <param name="lumaQuantizationParameter">The effective coding-unit luma quantization parameter before its bit-depth offset.</param>
    /// <param name="componentOffset">The combined picture, slice, and coding-unit component offset.</param>
    /// <param name="chromaBitDepthOffset">Six times the number of chroma bits above eight.</param>
    /// <param name="chromaFormat">The sequence chroma-format identifier.</param>
    /// <returns>The effective nonnegative chroma quantization parameter including its bit-depth offset.</returns>
    public static int GetChromaQuantizationParameter(
        int lumaQuantizationParameter,
        int componentOffset,
        int chromaBitDepthOffset,
        byte chromaFormat)
    {
        int unscaled = Math.Clamp(lumaQuantizationParameter + componentOffset, -chromaBitDepthOffset, 57);
        if (unscaled < 0)
        {
            return unscaled + chromaBitDepthOffset;
        }

        // H.265 section 8.6.1 maps nonnegative chroma QP before adding the bit-depth offset. The 4:2:0 table
        // contains plateaus above QP 29, whereas 4:2:2 and 4:4:4 remain linear through 51 and then saturate.
        int mapped = chromaFormat == 1
            ? Chroma420QuantizationParameterMap[unscaled]
            : Math.Min(unscaled, 51);

        return mapped + chromaBitDepthOffset;
    }
}
