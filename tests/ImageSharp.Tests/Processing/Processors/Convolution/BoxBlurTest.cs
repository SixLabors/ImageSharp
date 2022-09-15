// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution;

[Trait("Category", "Processors")]
[GroupOutput("Convolution")]
public class BoxBlurTest : Basic1ParameterConvolutionTests
{
    protected override void Apply(IImageProcessingContext ctx, int value) => ctx.BoxBlur(value);

    protected override void Apply(IImageProcessingContext ctx, int value, Rectangle bounds) =>
        ctx.BoxBlur(value, bounds);
}
