// <copyright file="PixelPoolTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.Linq;

    using Xunit;

    /// <summary>
    /// Tests the <see cref="PixelAccessor"/> class.
    /// </summary>
    public class PixelPoolTests
    {
        [Fact]
        public void PixelPoolRentsMinimumSize()
        {
            Color[] pixels = PixelPool<Color>.RentPixels(1024);

            Assert.True(pixels.Length >= 1024);
        }

        [Fact]
        public void PixelPoolRentsEmptyArray()
        {
            for (int i = 16; i < 1024; i += 16)
            {
                Color[] pixels = PixelPool<Color>.RentPixels(i);

                Assert.True(pixels.All(p => p == default(Color)));

                PixelPool<Color>.ReturnPixels(pixels);
            }

            for (int i = 16; i < 1024; i += 16)
            {
                Color[] pixels = PixelPool<Color>.RentPixels(i);

                Assert.True(pixels.All(p => p == default(Color)));

                PixelPool<Color>.ReturnPixels(pixels);
            }
        }

        [Fact]
        public void PixelPoolDoesNotThrowWhenReturningNonPooled()
        {
            Color[] pixels = new Color[1024];

            PixelPool<Color>.ReturnPixels(pixels);

            Assert.True(pixels.Length >= 1024);
        }

        [Fact]
        public void PixelPoolCleansRentedArray()
        {
            Color[] pixels = PixelPool<Color>.RentPixels(256);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.Azure;
            }

            Assert.True(pixels.All(p => p == Color.Azure));

            PixelPool<Color>.ReturnPixels(pixels);

            Assert.True(pixels.All(p => p == default(Color)));
        }
    }
}