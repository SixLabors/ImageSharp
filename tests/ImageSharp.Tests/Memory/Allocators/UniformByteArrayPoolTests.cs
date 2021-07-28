// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public class UniformByteArrayPoolTests
    {
        [Theory]
        [InlineData(3, 11)]
        [InlineData(7, 4)]
        public void Constructor_InitializesProperties(int arrayLength, int capacity)
        {
            var pool = new UniformByteArrayPool(arrayLength, capacity);
            Assert.Equal(arrayLength, pool.ArrayLength);
            Assert.Equal(capacity, pool.Capacity);
        }

        [Theory]
        [InlineData(1, 3)]
        [InlineData(8, 10)]
        public void Rent_SingleArray_ReturnsCorrectArray(int arrayLength, int capacity)
        {
            var pool = new UniformByteArrayPool(arrayLength, capacity);
            for (int i = 0; i < capacity; i++)
            {
                byte[] array = pool.Rent();
                Assert.NotNull(array);
                Assert.Equal(arrayLength, array.Length);
            }
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 5)]
        [InlineData(42, 7)]
        [InlineData(5, 10)]
        public void Rent_MultiArray_ReturnsCorrectArrays(int arrayLength, int arrayCount)
        {
            var pool = new UniformByteArrayPool(arrayLength, 10);
            byte[][] arrays = pool.Rent(arrayCount);
            Assert.NotNull(arrays);
            Assert.Equal(arrayCount, arrays.Length);

            foreach (byte[] array in arrays)
            {
                Assert.NotNull(array);
                Assert.Equal(arrayLength, array.Length);
            }
        }

        [Fact]
        public void Rent_MultipleTimesWithoutReturn_ReturnsDifferentArrays()
        {
            var pool = new UniformByteArrayPool(128, 10);
            byte[][] a = pool.Rent(2);
            byte[] b = pool.Rent();

            Assert.NotNull(a);

            Assert.NotNull(a[0]);
            Assert.NotNull(a[1]);
            Assert.NotNull(b);

            Assert.NotSame(a[0], a[1]);
            Assert.NotSame(a[0], b);
            Assert.NotSame(a[1], b);
        }

        [Fact]
        public void Return_IncorrectSize_ThrowsArgumentException()
        {
            var pool = new UniformByteArrayPool(128, 10);
            pool.Rent(5);

            byte[] c = new byte[127];
            byte[][] a = { new byte[127], new byte[129] };
            byte[][] b = { new byte[128], new byte[200] };

            Assert.Throws<ArgumentException>(() => pool.Return(c));
            Assert.Throws<ArgumentException>(() => pool.Return(a));
            Assert.Throws<ArgumentException>(() => pool.Return(b));
        }

        [Fact]
        public void Return_SingleArray_MoreThanRented_ThrowsInvalidOperationException()
        {
            var pool = new UniformByteArrayPool(2, 5);
            byte[] array = new byte[2];
            Assert.Throws<InvalidMemoryOperationException>(() => pool.Return(array));
        }

        [Fact]
        public void Return_MultiArray_MoreThanRented_ThrowsInvalidOperationException()
        {
            var pool = new UniformByteArrayPool(2, 5);
            pool.Rent(); // Rent 1 array

            byte[][] attempt1 = { new byte[2], new byte[2] };
            byte[][] attempt2 = { new byte[2], new byte[2], new byte[2] };
            Assert.Throws<InvalidMemoryOperationException>(() => pool.Return(attempt1));
            Assert.Throws<InvalidMemoryOperationException>(() => pool.Return(attempt2));
        }

        [Theory]
        [InlineData(4, 2, 10)]
        [InlineData(5, 1, 6)]
        [InlineData(12, 4, 12)]
        public void RentReturnRent_SameArrays(int totalCount, int rentUnit, int capacity)
        {
            var pool = new UniformByteArrayPool(128, capacity);
            var allArrays = new HashSet<byte[]>();
            var arrayUnits = new List<byte[][]>();

            byte[][] arrays;
            for (int i = 0; i < totalCount; i += rentUnit)
            {
                arrays = pool.Rent(rentUnit);
                Assert.NotNull(arrays);
                arrayUnits.Add(arrays);
                foreach (byte[] array in arrays)
                {
                    allArrays.Add(array);
                }
            }

            foreach (byte[][] arrayUnit in arrayUnits)
            {
                if (arrayUnit.Length == 1)
                {
                    // Test single-array return:
                    pool.Return(arrayUnit.Single());
                }
                else
                {
                    pool.Return(arrayUnit);
                }
            }

            arrays = pool.Rent(totalCount);

            Assert.NotNull(arrays);

            foreach (byte[] array in arrays)
            {
                Assert.Contains(array, allArrays);
            }
        }

        [Fact]
        public void Rent_SingleArray_OverCapacity_ReturnsNull()
        {
            var pool = new UniformByteArrayPool(7, 1000);
            Assert.NotNull(pool.Rent(1000));
            Assert.Null(pool.Rent());
        }

        [Theory]
        [InlineData(0, 6, 5)]
        [InlineData(5, 1, 5)]
        [InlineData(4, 7, 10)]
        public void Rent_MultiArray_OverCapacity_ReturnsNull(int initialRent, int attempt, int capacity)
        {
            var pool = new UniformByteArrayPool(128, capacity);
            Assert.NotNull(pool.Rent(initialRent));
            Assert.Null(pool.Rent(attempt));
        }

        [Theory]
        [InlineData(0, 5, 5)]
        [InlineData(5, 1, 6)]
        [InlineData(4, 7, 11)]
        [InlineData(3, 3, 7)]
        public void Rent_MultiArray_BelowCapacity_Succeeds(int initialRent, int attempt, int capacity)
        {
            var pool = new UniformByteArrayPool(128, capacity);
            Assert.NotNull(pool.Rent(initialRent));
            Assert.NotNull(pool.Rent(attempt));
        }

        [Fact]
        public void Release_Rent_Throws()
        {
            var pool = new UniformByteArrayPool(10, 3);
            pool.Release();
            Assert.Throws<InvalidMemoryOperationException>(() => pool.Rent());
            Assert.Throws<InvalidMemoryOperationException>(() => pool.Rent(2));
        }

        [Fact]
        public void Release_Return_Allowed()
        {
            var pool = new UniformByteArrayPool(10, 3);
            byte[] a = pool.Rent();
            byte[][] b = pool.Rent(2);
            pool.Release();
            pool.Return(a);
            pool.Return(b);
        }

        [Fact]
        public void RentReturn_IsThreadSafe()
        {
            int count = Environment.ProcessorCount * 200;
            var pool = new UniformByteArrayPool(8, count);
            var rnd = new Random(0);

            Parallel.For(0, Environment.ProcessorCount, i =>
            {
                var allArrays = new List<byte[]>();
                int pauseAt = rnd.Next(100);
                for (int j = 0; j < 100; j++)
                {
                    byte[][] data = pool.Rent(2);

                    data[0].AsSpan().Fill((byte)i);
                    data[1].AsSpan().Fill((byte)i);
                    allArrays.Add(data[0]);
                    allArrays.Add(data[1]);

                    if (j == pauseAt)
                    {
                        Thread.Sleep(15);
                    }
                }

                byte[] expected = new byte[8];
                expected.AsSpan().Fill((byte)i);

                foreach (byte[] array in allArrays)
                {
                    Assert.True(expected.SequenceEqual(array));
                    pool.Return(new[] { array });
                }
            });
        }

        private readonly ITestOutputHelper output;

        public UniformByteArrayPoolTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void GC_Collect_TrimsPoolAtExpectedRate()
        {
            RemoteExecutor.Invoke(RunTest).Dispose();

            void RunTest()
            {
                var pool = new UniformByteArrayPool(1024 * 1024, 128);
                UsePool(pool);

                GC.Collect();
                WaitOneTrim(); // 48
                WaitOneTrim(); // 36
                WaitOneTrim(); // 27
                WaitOneTrim(); // 20

                Assert.True(GC.GetTotalMemory(false) < 21L * 1024 * 1024);

                static void WaitOneTrim()
                {
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(30); // Wait for the trimming work item to complete on ThreadPool
                    GC.Collect();
                }

                static void UsePool(UniformByteArrayPool pool)
                {
                    byte[][] a1 = pool.Rent(16);
                    byte[][] a2 = pool.Rent(32);
                    pool.Return(a1);
                    byte[][] a3 = pool.Rent(16);
                    pool.Return(a2);
                    byte[][] a4 = pool.Rent(32);
                    byte[][] a5 = pool.Rent(16);
                    pool.Return(a4);
                    pool.Return(a3);
                    pool.Return(a5);

                    // Pool should retain 64MB at this point
                }
            }
        }
    }
}
