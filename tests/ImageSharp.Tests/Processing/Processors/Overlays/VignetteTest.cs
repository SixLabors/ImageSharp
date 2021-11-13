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

    public class VignetteTest_00 : VignetteTest { }
    public class VignetteTest_01 : VignetteTest { }
    public class VignetteTest_02 : VignetteTest { }
    public class VignetteTest_03 : VignetteTest { }
    public class VignetteTest_04 : VignetteTest { }
    public class VignetteTest_05 : VignetteTest { }
    public class VignetteTest_06 : VignetteTest { }
    public class VignetteTest_07 : VignetteTest { }
    public class VignetteTest_08 : VignetteTest { }
    public class VignetteTest_09 : VignetteTest { }
    public class VignetteTest_10 : VignetteTest { }
    public class VignetteTest_11 : VignetteTest { }
    public class VignetteTest_12 : VignetteTest { }
    public class VignetteTest_13 : VignetteTest { }
    public class VignetteTest_14 : VignetteTest { }
    public class VignetteTest_15 : VignetteTest { }
    public class VignetteTest_16 : VignetteTest { }
    public class VignetteTest_17 : VignetteTest { }
    public class VignetteTest_18 : VignetteTest { }
    public class VignetteTest_19 : VignetteTest { }
    public class VignetteTest_20 : VignetteTest { }
}
