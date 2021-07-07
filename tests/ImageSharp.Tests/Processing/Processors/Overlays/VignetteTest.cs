// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Overlays
{
    [Trait("Category", "Processors")]
    [GroupOutput("Overlays")]
    public class VignetteTest : OverlayTestBase
    {
        protected override void Apply(IImageProcessingContext ctx, Color color) => ctx.Vignette(color);

        protected override void Apply(IImageProcessingContext ctx, float radiusX, float radiusY) =>
            ctx.Vignette(radiusX, radiusY);

        protected override void Apply(IImageProcessingContext ctx, Rectangle rect) => ctx.Vignette(rect);
    }
}
