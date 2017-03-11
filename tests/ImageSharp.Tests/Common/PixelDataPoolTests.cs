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
        [Fact]
        public void PixelDataPoolRentsMinimumSize()
        {
            Color[] pixels = PixelDataPool<Color>.Rent(1024);

            Assert.True(pixels.Length >= 1024);
        }

        [Fact]
        public void PixelDataPoolDoesNotThrowWhenReturningNonPooled()
        {
            Color[] pixels = new Color[1024];

            PixelDataPool<Color>.Return(pixels);

            Assert.True(pixels.Length >= 1024);
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