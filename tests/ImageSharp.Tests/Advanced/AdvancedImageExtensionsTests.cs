// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Advanced
{
    public class AdvancedImageExtensionsTests
    {
        public class GetPixelMemory
        {
            [Theory]
            [WithSolidFilledImages(1, 1, "Red", PixelTypes.Rgba32)]
            [WithTestPatternImages(131, 127, PixelTypes.Rgba32 | PixelTypes.Bgr24)]
            public void WhenMemoryIsOwned<TPixel>(TestImageProvider<TPixel> provider)
                where TPixel : struct, IPixel<TPixel>
            {
                using (Image<TPixel> image0 = provider.GetImage())
                {
                    var targetBuffer = new TPixel[image0.Width * image0.Height];

                    // Act:
                    Memory<TPixel> memory = image0.GetRootFramePixelBuffer().GetSingleMemory();

                    // Assert:
                    Assert.Equal(image0.Width * image0.Height, memory.Length);
                    memory.Span.CopyTo(targetBuffer);

                    using (Image<TPixel> image1 = provider.GetImage())
                    {
                        // We are using a copy of the original image for assertion
                        image1.ComparePixelBufferTo(targetBuffer);
                    }
                }
            }

            [Theory]
            [WithSolidFilledImages(1, 1, "Red", PixelTypes.Rgba32 | PixelTypes.Bgr24)]
            [WithTestPatternImages(131, 127, PixelTypes.Rgba32 | PixelTypes.Bgr24)]
            public void WhenMemoryIsConsumed<TPixel>(TestImageProvider<TPixel> provider)
                where TPixel : struct, IPixel<TPixel>
            {
                using (Image<TPixel> image0 = provider.GetImage())
                {
                    var targetBuffer = new TPixel[image0.Width * image0.Height];
                    image0.GetPixelSpan().CopyTo(targetBuffer);

                    var managerOfExternalMemory = new TestMemoryManager<TPixel>(targetBuffer);

                    Memory<TPixel> externalMemory = managerOfExternalMemory.Memory;

                    using (var image1 = Image.WrapMemory(externalMemory, image0.Width, image0.Height))
                    {
                        Memory<TPixel> internalMemory = image1.GetRootFramePixelBuffer().GetSingleMemory();
                        Assert.Equal(targetBuffer.Length, internalMemory.Length);
                        Assert.True(Unsafe.AreSame(ref targetBuffer[0], ref internalMemory.Span[0]));

                        image0.ComparePixelBufferTo(internalMemory.Span);
                    }

                    // Make sure externalMemory works after destruction:
                    image0.ComparePixelBufferTo(externalMemory.Span);
                }
            }
        }

        [Theory]
        [WithSolidFilledImages(1, 1, "Red", PixelTypes.Rgba32)]
        [WithTestPatternImages(131, 127, PixelTypes.Rgba32 | PixelTypes.Bgr24)]
        public void GetPixelRowMemory<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var targetBuffer = new TPixel[image.Width * image.Height];

                // Act:
                for (int y = 0; y < image.Height; y++)
                {
                    Memory<TPixel> rowMemory = image.GetPixelRowMemory(y);
                    rowMemory.Span.CopyTo(targetBuffer.AsSpan(image.Width * y));
                }

                // Assert:
                using (Image<TPixel> image1 = provider.GetImage())
                {
                    // We are using a copy of the original image for assertion
                    image1.ComparePixelBufferTo(targetBuffer);
                }
            }
        }

        [Theory]
        [WithSolidFilledImages(1, 1, "Red", PixelTypes.Rgba32)]
        [WithTestPatternImages(131, 127, PixelTypes.Rgba32 | PixelTypes.Bgr24)]
        public void GetPixelRowSpan<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var targetBuffer = new TPixel[image.Width * image.Height];

                // Act:
                for (int y = 0; y < image.Height; y++)
                {
                    Span<TPixel> rowMemory = image.GetPixelRowSpan(y);
                    rowMemory.CopyTo(targetBuffer.AsSpan(image.Width * y));
                }

                // Assert:
                using (Image<TPixel> image1 = provider.GetImage())
                {
                    // We are using a copy of the original image for assertion
                    image1.ComparePixelBufferTo(targetBuffer);
                }
            }
        }
    }
}
