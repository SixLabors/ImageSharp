// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC luma and chroma quantization-parameter derivation.
/// </summary>
[Trait("Format", "Heic")]
public class HevcQuantizationParametersTests
{
    /// <summary>
    /// Verifies that luma quantization parameters include the precision-derived offset.
    /// </summary>
    /// <param name="quantizationParameter">The luma quantization parameter before the bit-depth offset.</param>
    /// <param name="bitDepth">The reconstructed luma precision.</param>
    /// <param name="expected">The expected effective luma quantization parameter.</param>
    [Theory]
    [InlineData(22, 8, 22)]
    [InlineData(-12, 10, 0)]
    [InlineData(-24, 12, 0)]
    [InlineData(51, 12, 75)]
    public void LumaIncludesBitDepthOffset(int quantizationParameter, int bitDepth, int expected)
    {
        HevcQuantizationParameters parameters = new(quantizationParameter, bitDepth, bitDepth, 1, 0, 0);

        Assert.Equal(expected, parameters.Luma);
        Assert.Equal(expected, parameters.Get(HevcPlane.Y));
    }

    /// <summary>
    /// Verifies the 4:2:0 chroma mapping plateaus and the upper mapped value.
    /// </summary>
    /// <param name="quantizationParameter">The luma quantization parameter before component offsets.</param>
    /// <param name="expected">The expected effective eight-bit chroma quantization parameter.</param>
    [Theory]
    [InlineData(29, 29)]
    [InlineData(30, 29)]
    [InlineData(35, 33)]
    [InlineData(37, 34)]
    [InlineData(43, 37)]
    [InlineData(51, 45)]
    public void Chroma420UsesNormativeMapping(int quantizationParameter, int expected)
    {
        HevcQuantizationParameters parameters = new(quantizationParameter, 8, 8, 1, 0, 0);

        Assert.Equal(expected, parameters.Cb);
        Assert.Equal(expected, parameters.Cr);
    }

    /// <summary>
    /// Verifies that 4:2:2 and 4:4:4 chroma quantization parameters are linear through 51 and saturate above it.
    /// </summary>
    /// <param name="chromaFormat">The tested sequence chroma-format identifier.</param>
    [Theory]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public void FullResolutionMappingsSaturateAboveFiftyOne(byte chromaFormat)
    {
        HevcQuantizationParameters parameters = new(51, 8, 8, chromaFormat, 6, 6);

        Assert.Equal(51, parameters.Cb);
        Assert.Equal(51, parameters.Cr);
    }

    /// <summary>
    /// Verifies negative chroma values and independent combined component offsets at higher precision.
    /// </summary>
    [Fact]
    public void ChromaAppliesCombinedOffsetsAndBitDepthOffset()
    {
        HevcQuantizationParameters parameters = new(-8, 10, 12, 1, -8, 20);

        Assert.Equal(4, parameters.Luma);
        Assert.Equal(8, parameters.Cb);
        Assert.Equal(36, parameters.Cr);
        Assert.Equal(parameters.Cb, parameters.Get(HevcPlane.Cb));
        Assert.Equal(parameters.Cr, parameters.Get(HevcPlane.Cr));
    }
}
