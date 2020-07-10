// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution
{
    [GroupOutput("Convolution")]
    public class GaussianBlurTest : Basic1ParameterConvolutionTests
    {
        protected override void Apply(IImageProcessingContext ctx, int value) => ctx.GaussianBlur(value);

        protected override void Apply(IImageProcessingContext ctx, int value, Rectangle bounds) =>
            ctx.GaussianBlur(value, bounds);
    }
}