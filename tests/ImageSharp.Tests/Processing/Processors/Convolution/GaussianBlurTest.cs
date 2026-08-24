// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution;

[Trait("Category", "Processors")]
[GroupOutput("Convolution")]
public class GaussianBlurTest : Basic1ParameterConvolutionTests
{
    protected override void Apply(IImageProcessingContext ctx, int value) => ctx.GaussianBlur(value);

    protected override void Apply(IImageProcessingContext ctx, int value, Rectangle bounds) =>
        ctx.GaussianBlur(bounds, value);

    [Theory]
    [InlineData(BorderWrappingMode.Repeat)]
    [InlineData(BorderWrappingMode.Mirror)]
    [InlineData(BorderWrappingMode.Bounce)]
    [InlineData(BorderWrappingMode.Wrap)]
    public void OnImageSmallerThanKernelRadius_LeavesSolidColorUnchanged(BorderWrappingMode mode)
    {
        // 8 rows against sigma 10 (61-tap kernel, radius 30). A normalized blur of a solid
        // image must remain that color no matter how the border sampling folds.
        Rgba32 red = new(255, 0, 0);
        using Image<Rgba32> image = new(130, 8, red);
        image.Mutate(x => x.GaussianBlur(image.Bounds, 10F, mode, mode));

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                foreach (Rgba32 pixel in accessor.GetRowSpan(y))
                {
                    Assert.InRange(pixel.R, 254, 255);
                    Assert.InRange(pixel.G, 0, 1);
                    Assert.InRange(pixel.B, 0, 1);
                    Assert.Equal(255, pixel.A);
                }
            }
        });
    }
}
