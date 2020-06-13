// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Advanced
{
    public class AdvancedImageExtensionsTests
    {
        public class GetPixelMemoryGroup
        {
            [Theory]
            [WithBasicTestPatternImages(1, 1, PixelTypes.Rgba32)]
            [WithBasicTestPatternImages(131, 127, PixelTypes.Rgba32)]
            [WithBasicTestPatternImages(333, 555, PixelTypes.Bgr24)]
            public void OwnedMemory_PixelDataIsCorrect<TPixel>(TestImageProvider<TPixel> provider)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                provider.LimitAllocatorBufferCapacity().InPixelsSqrt(200);

                using Image<TPixel> image = provider.GetImage();

                // Act:
                IMemoryGroup<TPixel> memoryGroup = image.GetPixelMemoryGroup();

                // Assert:
                VerifyMemoryGroupDataMatchesTestPattern(provider, memoryGroup, image.Size());
            }

            [Theory]
            [WithBlankImages(16, 16, PixelTypes.Rgba32)]
            public void OwnedMemory_DestructiveMutate_ShouldInvalidateMemoryGroup<TPixel>(TestImageProvider<TPixel> provider)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                using Image<TPixel> image = provider.GetImage();

                IMemoryGroup<TPixel> memoryGroup = image.GetPixelMemoryGroup();
                Memory<TPixel> memory = memoryGroup.Single();

                image.Mutate(c => c.Resize(8, 8));

                Assert.False(memoryGroup.IsValid);
                Assert.ThrowsAny<InvalidMemoryOperationException>(() => _ = memoryGroup.First());
                Assert.ThrowsAny<InvalidMemoryOperationException>(() => _ = memory.Span);
            }

            [Theory]
            [WithBasicTestPatternImages(1, 1, PixelTypes.Rgba32)]
            [WithBasicTestPatternImages(131, 127, PixelTypes.Bgr24)]
            public void ConsumedMemory_PixelDataIsCorrect<TPixel>(TestImageProvider<TPixel> provider)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                using Image<TPixel> image0 = provider.GetImage();
                var targetBuffer = new TPixel[image0.Width * image0.Height];

                Assert.True(image0.TryGetSinglePixelSpan(out Span<TPixel> sourceBuffer));

                sourceBuffer.CopyTo(targetBuffer);

                var managerOfExternalMemory = new TestMemoryManager<TPixel>(targetBuffer);

                Memory<TPixel> externalMemory = managerOfExternalMemory.Memory;

                using (var image1 = Image.WrapMemory(externalMemory, image0.Width, image0.Height))
                {
                    VerifyMemoryGroupDataMatchesTestPattern(provider, image1.GetPixelMemoryGroup(), image1.Size());
                }

                // Make sure externalMemory works after destruction:
                VerifyMemoryGroupDataMatchesTestPattern(provider, image0.GetPixelMemoryGroup(), image0.Size());
            }

            private static void VerifyMemoryGroupDataMatchesTestPattern<TPixel>(
                TestImageProvider<TPixel> provider,
                IMemoryGroup<TPixel> memoryGroup,
                Size size)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                Assert.True(memoryGroup.IsValid);
                Assert.Equal(size.Width * size.Height, memoryGroup.TotalLength);
                Assert.True(memoryGroup.BufferLength % size.Width == 0);

                int cnt = 0;
                for (MemoryGroupIndex i = memoryGroup.MaxIndex(); i < memoryGroup.MaxIndex(); i += 1, cnt++)
                {
                    int y = cnt / size.Width;
                    int x = cnt % size.Width;

                    TPixel expected = provider.GetExpectedBasicTestPatternPixelAt(x, y);
                    TPixel actual = memoryGroup.GetElementAt(i);
                    Assert.Equal(expected, actual);
                }
            }
        }

        [Theory]
        [WithBasicTestPatternImages(1, 1, PixelTypes.Rgba32)]
        [WithBasicTestPatternImages(131, 127, PixelTypes.Rgba32)]
        [WithBasicTestPatternImages(333, 555, PixelTypes.Bgr24)]
        public void GetPixelRowMemory_PixelDataIsCorrect<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(200);

            using Image<TPixel> image = provider.GetImage();

            for (int y = 0; y < image.Height; y++)
            {
                // Act:
                Memory<TPixel> rowMemory = image.GetPixelRowMemory(y);
                Span<TPixel> span = rowMemory.Span;

                // Assert:
                for (int x = 0; x < image.Width; x++)
                {
                    Assert.Equal(provider.GetExpectedBasicTestPatternPixelAt(x, y), span[x]);
                }
            }
        }

        [Theory]
        [WithBasicTestPatternImages(16, 16, PixelTypes.Rgba32)]
        public void GetPixelRowMemory_DestructiveMutate_ShouldInvalidateMemory<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();

            Memory<TPixel> memory3 = image.GetPixelRowMemory(3);
            Memory<TPixel> memory10 = image.GetPixelRowMemory(10);

            image.Mutate(c => c.Resize(8, 8));

            Assert.ThrowsAny<InvalidMemoryOperationException>(() => _ = memory3.Span);
            Assert.ThrowsAny<InvalidMemoryOperationException>(() => _ = memory10.Span);
        }

        [Theory]
        [WithBlankImages(1, 1, PixelTypes.Rgba32)]
        [WithBlankImages(100, 111, PixelTypes.Rgba32)]
        [WithBlankImages(400, 600, PixelTypes.Rgba32)]
        public void GetPixelRowSpan_ShouldReferenceSpanOfMemory<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(200);

            using Image<TPixel> image = provider.GetImage();

            Memory<TPixel> memory = image.GetPixelRowMemory(image.Height - 1);
            Span<TPixel> span = image.GetPixelRowSpan(image.Height - 1);

            Assert.True(span == memory.Span);
        }
    }
}
