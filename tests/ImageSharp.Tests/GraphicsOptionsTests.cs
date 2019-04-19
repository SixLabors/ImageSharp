// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class GraphicsOptionsTests
    {
        [Fact]
        public void IsOpaqueColor()
        {
            Assert.True(new GraphicsOptions(true).IsOpaqueColorWithoutBlending(Rgba32.Red));
            Assert.False(new GraphicsOptions(true, 0.5f).IsOpaqueColorWithoutBlending(Rgba32.Red));
            Assert.False(new GraphicsOptions(true).IsOpaqueColorWithoutBlending(Rgba32.Transparent));
            Assert.False(new GraphicsOptions(true, PixelColorBlendingMode.Lighten, 1).IsOpaqueColorWithoutBlending(Rgba32.Red));
            Assert.False(new GraphicsOptions(true, PixelColorBlendingMode.Normal, PixelAlphaCompositionMode.DestOver, 1).IsOpaqueColorWithoutBlending(Rgba32.Red));
        }
    }
}