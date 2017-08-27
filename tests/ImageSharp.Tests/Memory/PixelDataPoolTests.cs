// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.



// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    using SixLabors.ImageSharp.Memory;

    using Xunit;

    /// <summary>
    /// Tests the <see cref="PixelDataPool{T}"/> class.
    /// </summary>
    public class PixelDataPoolTests
    {
        [Fact]
        public void PixelDataPoolRentsMinimumSize()
        {
            Rgba32[] pixels = PixelDataPool<Rgba32>.Rent(1024);

            Assert.True(pixels.Length >= 1024);
        }

        [Fact]
        public void PixelDataPoolDoesNotThrowWhenReturningNonPooled()
        {
            Rgba32[] pixels = new Rgba32[1024];

            PixelDataPool<Rgba32>.Return(pixels);

            Assert.True(pixels.Length >= 1024);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CalculateMaxArrayLength(bool isRawData)
        {
            int max = isRawData ? PixelDataPool<int>.CalculateMaxArrayLength()
                          : PixelDataPool<Rgba32>.CalculateMaxArrayLength();

            Assert.Equal(max > 1024 * 1024, !isRawData);
        }

        [Fact]
        public void RentNonIPixelData()
        {
            byte[] data = PixelDataPool<byte>.Rent(16384);

            Assert.True(data.Length >= 16384);
        }
    }
}