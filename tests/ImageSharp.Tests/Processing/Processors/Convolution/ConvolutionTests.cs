// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Extensions.Convolution;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution;

[GroupOutput("Convolution")]
public class ConvolutionTests
{
    private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.05F);

    public static readonly TheoryData<DenseMatrix<float>> Values = new()
    {
        // Sharpening kernel.
        new float[,]
        {
            { -1, -1, -1 },
            { -1, 16, -1 },
            { -1, -1, -1 }
        }
    };

    public static readonly string[] InputImages =
    [
        TestImages.Bmp.Car,
        TestImages.Png.CalliphoraPartial,
        TestImages.Png.Blur
    ];

    [Theory]
    [WithFileCollection(nameof(InputImages), nameof(Values), PixelTypes.Rgba32)]
    public void OnFullImage<TPixel>(TestImageProvider<TPixel> provider, DenseMatrix<float> value)
        where TPixel : unmanaged, IPixel<TPixel>
        => provider.RunValidatingProcessorTest(
            x => x.Convolve(value),
            string.Join('_', value.Data),
            ValidatorComparer);

    [Theory]
    [WithFileCollection(nameof(InputImages), nameof(Values), PixelTypes.Rgba32)]
    public void InBox<TPixel>(TestImageProvider<TPixel> provider, DenseMatrix<float> value)
        where TPixel : unmanaged, IPixel<TPixel>
        => provider.RunRectangleConstrainedValidatingProcessorTest(
            (x, rect) => x.Convolve(rect, value),
            string.Join('_', value.Data),
            ValidatorComparer);
}
