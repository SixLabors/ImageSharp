// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Overlays
{
    [GroupOutput("Overlays")]
    public class VignetteTest : OverlayTestBase
    {
        protected override void Apply<T>(IImageProcessingContext<T> ctx, T color) => ctx.Vignette(color);

        protected override void Apply<T>(IImageProcessingContext<T> ctx, float radiusX, float radiusY) =>
            ctx.Vignette(radiusX, radiusY);

        protected override void Apply<T>(IImageProcessingContext<T> ctx, Rectangle rect) => ctx.Vignette(rect);
    }
}