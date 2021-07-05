// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory.Internals;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public class UniformByteArrayPoolTests
    {
        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 5)]
        [InlineData(42, 7)]
        public void TryRent_ReturnsCorrectArrays(int arraySize, int count)
        {
            var pool = new UniformByteArrayPool(arraySize, 10);
            byte[][] arrays = new byte[count][];
            Assert.True(pool.TryRent(arrays));

            foreach (byte[] array in arrays)
            {
                Assert.NotNull(array);
                Assert.Equal(arraySize, array.Length);
            }
        }

        [Fact]
        public void TryRent_MultipleTimes_ReturnsDifferentArrays()
        {
            var pool = new UniformByteArrayPool(128, 10);
            byte[][] a = new byte[3][];

            Assert.True(pool.TryRent(a.AsSpan(0, 2)));
            Assert.True(pool.TryRent(a.AsSpan(2, 1)));

            Assert.NotNull(a[0]);
            Assert.NotNull(a[1]);
            Assert.NotNull(a[2]);

            Assert.NotSame(a[0], a[1]);
            Assert.NotSame(a[0], a[2]);
            Assert.NotSame(a[1], a[2]);
        }

        [Fact]
        public void Return_IncorrectSize_ThrowsArgumentException()
        {
            var pool = new UniformByteArrayPool(128, 10);
            byte[][] a = { new byte[127], new byte[129] };
            byte[][] b = { new byte[128], new byte[200] };

            Assert.Throws<ArgumentException>(() => pool.Return(a));
            Assert.Throws<ArgumentException>(() => pool.Return(b));
        }

        [Theory]
        [InlineData(4, 2, 10)]
        [InlineData(12, 4, 12)]
        public void RentReturnRent_SameArrays(int totalCount, int rentUnit, int capacity)
        {
            var pool = new UniformByteArrayPool(128, capacity);
            var allArrays = new HashSet<byte[]>();
            var arrayUnits = new List<byte[][]>();

            byte[][] arrays = new byte[rentUnit][];
            for (int i = 0; i < totalCount; i += rentUnit)
            {
                Assert.True(pool.TryRent(arrays));
                arrayUnits.Add(arrays);
                foreach (byte[] array in arrays)
                {
                    allArrays.Add(array);
                }
            }

            foreach (byte[][] arrayUnit in arrayUnits)
            {
                pool.Return(arrayUnit);
            }

            arrays = new byte[totalCount][];
            Assert.True(pool.TryRent(arrays));

            foreach (byte[] array in arrays)
            {
                Assert.Contains(array, allArrays);
            }
        }

        [Theory]
        [InlineData(0, 6, 5)]
        [InlineData(5, 1, 5)]
        [InlineData(4, 7, 10)]
        public void TryRent_OverCapacity_ReturnsFalse(int initialRent, int attempt, int capacity)
        {
            var pool = new UniformByteArrayPool(128, capacity);
            byte[][] arrays = new byte[initialRent][];
            Assert.True(pool.TryRent(arrays));
            arrays = new byte[attempt][];
            Assert.False(pool.TryRent(arrays));
            foreach (byte[] array in arrays)
            {
                Assert.Null(array);
            }
        }

        [Fact]
        public void RentReturn_IsThreadSafe()
        {
            int count = Environment.ProcessorCount * 200;
            var pool = new UniformByteArrayPool(8, count);
            var rnd = new Random(0);

            Parallel.For(0, Environment.ProcessorCount, i =>
            {
                byte[][] data = new byte[2][];
                var allArrays = new List<byte[]>();
                int stopAt = rnd.Next(100);
                for (int j = 0; j < 100; j++)
                {
                    Assert.True(pool.TryRent(data));
                    data[0].AsSpan().Fill((byte)i);
                    data[1].AsSpan().Fill((byte)i);
                    allArrays.Add(data[0]);
                    allArrays.Add(data[1]);

                    if (j == stopAt)
                    {
                        Thread.Sleep(15);
                    }
                }

                byte[] expected = { (byte)i, (byte)i };
                foreach (byte[] array in allArrays)
                {
                    Assert.True(expected.SequenceEqual(array));
                    pool.Return(new[] { array });
                }
            });
        }
    }
}
