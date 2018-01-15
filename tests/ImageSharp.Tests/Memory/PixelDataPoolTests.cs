// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    /// <summary>
    /// Tests the <see cref="PixelDataPool{T}"/> class.
    /// </summary>
    public class PixelDataPoolTests
    {
        readonly object monitor = new object();
        
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

        [Fact]
        public void SmallBuffersArePooled()
        {
            Assert.True(this.CheckIsPooled<byte>(5, 512));
        }

        [Fact]
        public void LargeBuffersAreNotPooled_OfByte()
        {
            const int mb128 = 128 * 1024 * 1024;
            Assert.False(this.CheckIsPooled<byte>(2, mb128));
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