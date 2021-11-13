// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Overlays
{
    [Trait("Category", "Processors")]
    [GroupOutput("Overlays")]
    public class GlowTest : OverlayTestBase
    {
        protected override void Apply(IImageProcessingContext ctx, Color color) => ctx.Glow(color);

        protected override void Apply(IImageProcessingContext ctx, float radiusX, float radiusY) =>
            ctx.Glow(radiusX);

        protected override void Apply(IImageProcessingContext ctx, Rectangle rect) => ctx.Glow(rect);
    }

    public class GlowTest_00 : GlowTest { }
    public class GlowTest_01 : GlowTest { }
    public class GlowTest_02 : GlowTest { }
    public class GlowTest_03 : GlowTest { }
    public class GlowTest_04 : GlowTest { }
    public class GlowTest_05 : GlowTest { }
    public class GlowTest_06 : GlowTest { }
    public class GlowTest_07 : GlowTest { }
    public class GlowTest_08 : GlowTest { }
    public class GlowTest_09 : GlowTest { }
    public class GlowTest_10 : GlowTest { }
    public class GlowTest_11 : GlowTest { }
    public class GlowTest_12 : GlowTest { }
    public class GlowTest_13 : GlowTest { }
    public class GlowTest_14 : GlowTest { }
    public class GlowTest_15 : GlowTest { }
    public class GlowTest_16 : GlowTest { }
    public class GlowTest_17 : GlowTest { }
    public class GlowTest_18 : GlowTest { }
    public class GlowTest_19 : GlowTest { }
    public class GlowTest_20 : GlowTest { }
}
