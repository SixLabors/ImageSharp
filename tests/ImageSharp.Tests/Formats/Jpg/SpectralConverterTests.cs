// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public class SpectralConverterTests
{
    // Test for null target size, i.e. when no scaling is needed
    [Theory]
    [InlineData(1, 1)]
    [InlineData(800, 400)]
    [InlineData(2354, 4847)]
    public void CalculateResultingImageSize_Null_TargetSize(int width, int height)
    {
        Size inputSize = new(width, height);

        Size outputSize = SpectralConverter.CalculateResultingImageSize(inputSize, null, 1, out int blockPixelSize);

        Assert.Equal(expected: 8, blockPixelSize);
        Assert.Equal(inputSize, outputSize);
    }

    // Test for 'perfect' dimensions, i.e. dimensions divisible by 8, with exact scaled size match
    [Theory]
    [InlineData(800, 400, 800, 400, 8)]
    [InlineData(800, 400, 400, 200, 4)]
    [InlineData(800, 400, 200, 100, 2)]
    [InlineData(800, 400, 100, 50, 1)]
    public void CalculateResultingImageSize_Perfect_Dimensions_Exact_Match(int inW, int inH, int tW, int tH, int expectedBlockSize)
    {
        Size inputSize = new(inW, inH);
        Size targetSize = new(tW, tH);

        Size outputSize = SpectralConverter.CalculateResultingImageSize(inputSize, targetSize, 1, out int blockPixelSize);

        Assert.Equal(expectedBlockSize, blockPixelSize);
        Assert.Equal(outputSize, targetSize);
    }

    // Test for 'imperfect' dimensions, i.e. dimensions NOT divisible by 8, with exact scaled size match
    [Theory]
    [InlineData(7, 14, 7, 14, 8)]
    [InlineData(7, 14, 4, 7, 4)]
    [InlineData(7, 14, 2, 4, 2)]
    [InlineData(7, 14, 1, 2, 1)]
    public void CalculateResultingImageSize_Imperfect_Dimensions_Exact_Match(int inW, int inH, int tW, int tH, int expectedBlockSize)
    {
        Size inputSize = new(inW, inH);
        Size targetSize = new(tW, tH);

        Size outputSize = SpectralConverter.CalculateResultingImageSize(inputSize, targetSize, 1, out int blockPixelSize);

        Assert.Equal(expectedBlockSize, blockPixelSize);
        Assert.Equal(outputSize, targetSize);
    }

    // Test for inexact target and output sizes match
    [Theory]
    [InlineData(7, 14, 4, 6, 4, 7, 4)]
    [InlineData(7, 14, 1, 1, 1, 2, 1)]
    [InlineData(800, 400, 999, 600, 800, 400, 8)]
    [InlineData(800, 400, 390, 150, 400, 200, 4)]
    [InlineData(804, 1198, 500, 800, 804, 1198, 8)]

    // TargetSize uses ResizeMode.Max: 1280x853 fits into 150x150 as 150x100, so 1/8 scale (160x107) is sufficient.
    [InlineData(1280, 853, 150, 150, 160, 107, 1)]
    public void CalculateResultingImageSize_Inexact_Target_Size(int inW, int inH, int tW, int tH, int exW, int exH, int expectedBlockSize)
    {
        Size inputSize = new(inW, inH);
        Size targetSize = new(tW, tH);
        Size expectedSize = new(exW, exH);

        Size outputSize = SpectralConverter.CalculateResultingImageSize(inputSize, targetSize, 1, out int blockPixelSize);

        Assert.Equal(expectedBlockSize, blockPixelSize);
        Assert.Equal(expectedSize, outputSize);
    }

    // When a resize follows the scaled decode, the scaled size keeps at least two times the fitted target size.
    [Theory]
    [InlineData(1280, 853, 150, 150, 320, 214, 2)]
    [InlineData(1280, 1280, 150, 150, 320, 320, 2)]
    [InlineData(800, 400, 390, 150, 800, 400, 8)]
    public void CalculateResultingImageSize_ResizeFactor_KeepsTwiceFittedSize(int inW, int inH, int tW, int tH, int exW, int exH, int expectedBlockSize)
    {
        Size inputSize = new(inW, inH);
        Size targetSize = new(tW, tH);
        Size expectedSize = new(exW, exH);

        Size outputSize = SpectralConverter.CalculateResultingImageSize(inputSize, targetSize, 2, out int blockPixelSize);

        Assert.Equal(expectedBlockSize, blockPixelSize);
        Assert.Equal(expectedSize, outputSize);
    }
}
