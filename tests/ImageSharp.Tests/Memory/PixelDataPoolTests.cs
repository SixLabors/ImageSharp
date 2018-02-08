// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    using System;

    /// <summary>
    /// Tests the <see cref="PixelDataPool{T}"/> class.
    /// </summary>
    public class PixelDataPoolTests
    {
        private const int MaxPooledBufferSizeInBytes = PixelDataPool<byte>.MaxPooledBufferSizeInBytes;

        readonly object monitor = new object();
        
        [Theory]
        [InlineData(1)]
        [InlineData(1024)]
        public void PixelDataPoolRentsMinimumSize(int size)
        {
            Rgba32[] pixels = PixelDataPool<Rgba32>.Rent(size);

            Assert.True(pixels.Length >= size);
        }

        [Fact]
        public void PixelDataPoolDoesNotThrowWhenReturningNonPooled()
        {
            Rgba32[] pixels = new Rgba32[1024];

            PixelDataPool<Rgba32>.Return(pixels);

            Assert.True(pixels.Length >= 1024);
        }

        /// <summary>
        /// Rent 'n' buffers -> return all -> re-rent, verify if there is at least one in common.
        /// </summary>
        private bool CheckIsPooled<T>(int n, int count)
            where T : struct
        {
            lock (this.monitor)
            {
                T[][] original = new T[n][];

                for (int i = 0; i < n; i++)
                {
                    original[i] = PixelDataPool<T>.Rent(count);
                }

                for (int i = 0; i < n; i++)
                {
                    PixelDataPool<T>.Return(original[i]);
                }

                T[][] verification = new T[n][];

                for (int i = 0; i < n; i++)
                {
                    verification[i] = PixelDataPool<T>.Rent(count);
                }

                return original.Intersect(verification).Any();
            }
        }

        [Theory]
        [InlineData(32)]
        [InlineData(512)]
        [InlineData(MaxPooledBufferSizeInBytes-1)]
        public void SmallBuffersArePooled(int size)
        {
            Assert.True(this.CheckIsPooled<byte>(5, size));
        }

        [Theory]
        [InlineData(128 * 1024 * 1024)]
        [InlineData(MaxPooledBufferSizeInBytes+1)]
        public void LargeBuffersAreNotPooled_OfByte(int size)
        {
            Assert.False(this.CheckIsPooled<byte>(2, size));
        }

        [StructLayout(LayoutKind.Explicit, Size = 512)]
        struct TestStruct
        {
        }

        [Fact]
        public unsafe void LaregeBuffersAreNotPooled_OfBigValueType()
        {
            const int mb128 = 128 * 1024 * 1024;
            int count = mb128 / sizeof(TestStruct);

            Assert.False(this.CheckIsPooled<TestStruct>(2, count));
        }
    }
}