// <copyright file="PixelDataPoolTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.Linq;

    using Xunit;

    /// <summary>
    /// Tests the <see cref="PixelDataPool{T}"/> class.
    /// </summary>
    public class PixelDataPoolTests
    {
        [Fact]
        public void PixelDataPoolRentsMinimumSize()
        {
            Color[] pixels = PixelDataPool<Color>.Rent(1024);

            Assert.True(pixels.Length >= 1024);
        }

        [Fact]
        public void PixelDataPoolRentsEmptyArray()
        {
            for (int i = 16; i < 1024; i += 16)
            {
                Color[] pixels = PixelDataPool<Color>.Rent(i);

                Assert.True(pixels.All(p => p == default(Color)));

                PixelDataPool<Color>.Return(pixels);
            }

            for (int i = 16; i < 1024; i += 16)
            {
                Color[] pixels = PixelDataPool<Color>.Rent(i);

                Assert.True(pixels.All(p => p == default(Color)));

                PixelDataPool<Color>.Return(pixels);
            }
        }

        [Fact]
        public void PixelDataPoolDoesNotThrowWhenReturningNonPooled()
        {
            Color[] pixels = new Color[1024];

            PixelDataPool<Color>.Return(pixels);

            Assert.True(pixels.Length >= 1024);
        }

        [Fact]
        public void PixelDataPoolCleansRentedArray()
        {
            Color[] pixels = PixelDataPool<Color>.Rent(256);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.Azure;
            }

            Assert.True(pixels.All(p => p == Color.Azure));

            PixelDataPool<Color>.Return(pixels);

            Assert.True(pixels.All(p => p == default(Color)));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CalculateMaxArrayLength(bool isRawData)
        {
            int max = isRawData ? PixelDataPool<int>.CalculateMaxArrayLength()
                          : PixelDataPool<Color>.CalculateMaxArrayLength();

            Assert.Equal(max < int.MaxValue, !isRawData);
        }

        [Fact]
        public void RentNonIPixelData()
        {
            byte[] data = PixelDataPool<byte>.Rent(16384);

            Assert.True(data.Length >= 16384);
        }
    }
}