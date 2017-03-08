// <copyright file="PixelDataPoolTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using System.Linq;

    using Xunit;

    /// <summary>
    /// Tests the <see cref="PixelDataPool{T}"/> class.
    /// </summary>
    public class PixelDataPoolTests
    {
        private static PixelDataPool<Color> GetPool(bool clean)
        {
            return clean ? PixelDataPool<Color>.Clean : PixelDataPool<Color>.Dirty;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PixelDataPoolRentsMinimumSize(bool clean)
        {
            Color[] pixels = GetPool(clean).Rent(1024);

            Assert.True(pixels.Length >= 1024);
        }

        [Fact]
        public void PixelDataPool_Clean_RentsCleanArray()
        {
            for (int i = 16; i < 1024; i += 16)
            {
                Color[] pixels = PixelDataPool<Color>.Clean.Rent(i);

                Assert.True(pixels.All(p => p == default(Color)));

                PixelDataPool<Color>.Clean.Return(pixels);
            }

            for (int i = 16; i < 1024; i += 16)
            {
                Color[] pixels = PixelDataPool<Color>.Clean.Rent(i);

                Assert.True(pixels.All(p => p == default(Color)));

                PixelDataPool<Color>.Clean.Return(pixels);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PixelDataPoolDoesNotThrowWhenReturningNonPooled(bool clean)
        {
            Color[] pixels = new Color[1024];

            GetPool(clean).Return(pixels);

            Assert.True(pixels.Length >= 1024);
        }

        [Fact]
        public void PixelDataPool_Clean_CleansRentedArray()
        {
            Color[] pixels = PixelDataPool<Color>.Clean.Rent(256);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.Azure;
            }

            Assert.True(pixels.All(p => p == Color.Azure));

            PixelDataPool<Color>.Clean.Return(pixels);

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
            byte[] data = PixelDataPool<byte>.Clean.Rent(16384);

            Assert.True(data.Length >= 16384);
        }
    }
}