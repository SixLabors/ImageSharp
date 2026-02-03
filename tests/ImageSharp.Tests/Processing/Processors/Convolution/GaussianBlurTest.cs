// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution;

[Trait("Category", "Processors")]
[GroupOutput("Convolution")]
public class GaussianBlurTest : Basic1ParameterConvolutionTests
{
    protected override void Apply(IImageProcessingContext ctx, int value) => ctx.GaussianBlur(value);

    protected override void Apply(IImageProcessingContext ctx, int value, Rectangle bounds) =>
        ctx.GaussianBlur(bounds, value);
}
