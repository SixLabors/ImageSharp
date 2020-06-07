// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class LargeImageIntegrationTests
    {
        [Theory(Skip = "For local testing only.")]
        [WithBasicTestPatternImages(width: 30000, height: 30000, PixelTypes.Rgba32)]
        public void CreateAndResize(TestImageProvider<Rgba32> provider)
        {
            using Image<Rgba32> image = provider.GetImage();
            image.Mutate(c => c.Resize(1000, 1000));
            image.DebugSave(provider);
        }

        [Theory]
        [WithBasicTestPatternImages(width: 10, height: 10, PixelTypes.Rgba32)]
        public void GetSingleSpan(TestImageProvider<Rgba32> provider)
        {
            provider.LimitAllocatorBufferCapacity().InPixels(10);
            using Image<Rgba32> image = provider.GetImage();
            Assert.False(image.TryGetSinglePixelSpan(out Span<Rgba32> imageSpan));
            Assert.False(image.Frames.RootFrame.TryGetSinglePixelSpan(out Span<Rgba32> imageFrameSpan));
        }
    }
}
