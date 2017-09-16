// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using Xunit;
using SixLabors.ImageSharp.Advanced;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Tests.Advanced
{
    public class ImageExtensionsTests
    {
        [Fact]
        public unsafe void DangerousGetPinnableReference_CopyToBuffer()
        {
            var image = new Image<Rgba32>(128, 128);
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    image[x, y] = new Rgba32(x, 255 - y, x + y);
                }

            Rgba32[] targetBuffer = new Rgba32[image.Width * image.Height];

            fixed (Rgba32* targetPtr = targetBuffer)
            fixed (Rgba32* pixelBasePtr = &image.DangerousGetPinnableReferenceToPixelBuffer())
            {
                uint dataSizeInBytes = (uint)(image.Width * image.Height * Unsafe.SizeOf<Rgba32>());
                Unsafe.CopyBlock(targetPtr, pixelBasePtr, dataSizeInBytes);
            }

            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    int linearIndex = y * image.Width + x;
                    Assert.Equal(image[x, y], targetBuffer[linearIndex]);
                }
        }
    }
}
