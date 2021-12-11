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
        public void PreferContiguousImageBuffers_CreateImage_BufferIsContiguous()
        {
            // Run remotely to avoid large allocation in the test process:
            RemoteExecutor.Invoke(RunTest).Dispose();

            static void RunTest()
            {
                Configuration configuration = Configuration.Default.Clone();
                configuration.PreferContiguousImageBuffers = true;

                using var image = new Image<Rgba32>(configuration, 8192, 4096);
                Assert.True(image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem));
                Assert.Equal(8192 * 4096, mem.Length);
            }
        }

        [Theory]
        [WithBasicTestPatternImages(width: 10, height: 10, PixelTypes.Rgba32)]
        public void DangerousTryGetSinglePixelMemory_WhenImageTooLarge_ReturnsFalse(TestImageProvider<Rgba32> provider)
        {
            provider.LimitAllocatorBufferCapacity().InPixels(10);
            using Image<Rgba32> image = provider.GetImage();
            Assert.False(image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem));
            Assert.False(image.Frames.RootFrame.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> _));
        }
    }
}
