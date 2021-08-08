// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory;
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

        [Fact]
        public unsafe void Set_MaximumPoolSizeMegabytes_CreateImage_MaximumPoolSizeMegabytes()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();

            static void RunTest()
            {
                Configuration.Default.MemoryAllocator = MemoryAllocator.CreateDefault(new MemoryAllocatorOptions()
                {
                    MinimumContiguousBlockBytes = sizeof(Rgba32) * 8192 * 4096
                });
                using Image<Rgba32> image = new Image<Rgba32>(8192, 4096);
                Assert.True(image.TryGetSinglePixelSpan(out Span<Rgba32> span));
                Assert.Equal(8192 * 4096, span.Length);
            }
        }

        [Theory]
        [WithBasicTestPatternImages(width: 10, height: 10, PixelTypes.Rgba32)]
        public void TryGetSinglePixelSpan_WhenImageTooLarge_ReturnsFalse(TestImageProvider<Rgba32> provider)
        {

            provider.LimitAllocatorBufferCapacity().InPixels(10);
            using Image<Rgba32> image = provider.GetImage();
            Assert.False(image.TryGetSinglePixelSpan(out Span<Rgba32> imageSpan));
            Assert.False(image.Frames.RootFrame.TryGetSinglePixelSpan(out Span<Rgba32> imageFrameSpan));
        }
    }
}
