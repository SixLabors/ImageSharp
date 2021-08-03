// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Memory.Internals;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    public class UniformUnmanagedMemoryPoolTests
    {
        private readonly ITestOutputHelper output;

        public UniformUnmanagedMemoryPoolTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private static unsafe Span<byte> GetSpan(UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle h) =>
            new Span<byte>((void*)h.DangerousGetHandle(), pool.BufferLength);

        [Theory]
        [InlineData(3, 11)]
        [InlineData(7, 4)]
        public void Constructor_InitializesProperties(int arrayLength, int capacity)
        {
            var pool = new UniformUnmanagedMemoryPool(arrayLength, capacity);
            Assert.Equal(arrayLength, pool.BufferLength);
            Assert.Equal(capacity, pool.Capacity);
        }

        [Theory]
        [InlineData(1, 3)]
        [InlineData(8, 10)]
        public void Rent_SingleBuffer_ReturnsCorrectBuffer(int length, int capacity)
        {
            var pool = new UniformUnmanagedMemoryPool(length, capacity);
            for (int i = 0; i < capacity; i++)
            {
                UnmanagedMemoryHandle h = pool.Rent();
                CheckBuffer(length, pool, h);
            }
        }

        private static void CheckBuffer(int length, UniformUnmanagedMemoryPool pool, UnmanagedMemoryHandle h)
        {
            Assert.NotNull(h);
            Span<byte> span = GetSpan(pool, h);
            span.Fill(123);

            byte[] expected = new byte[length];
            expected.AsSpan().Fill(123);
            Assert.True(span.SequenceEqual(expected));
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 5)]
        [InlineData(42, 7)]
        [InlineData(5, 10)]
        public void Rent_MultiBuffer_ReturnsCorrectBuffers(int length, int bufferCount)
        {
            var pool = new UniformUnmanagedMemoryPool(length, 10);
            UnmanagedMemoryHandle[] handles = pool.Rent(bufferCount);
            Assert.NotNull(handles);
            Assert.Equal(bufferCount, handles.Length);

            foreach (UnmanagedMemoryHandle h in handles)
            {
                CheckBuffer(length, pool, h);
            }
        }

        [Fact]
        public void Rent_MultipleTimesWithoutReturn_ReturnsDifferentHandles()
        {
            var pool = new UniformUnmanagedMemoryPool(128, 10);
            UnmanagedMemoryHandle[] a = pool.Rent(2);
            UnmanagedMemoryHandle b = pool.Rent();

            Assert.NotEqual(a[0].DangerousGetHandle(), a[1].DangerousGetHandle());
            Assert.NotEqual(a[0].DangerousGetHandle(), b.DangerousGetHandle());
            Assert.NotEqual(a[1].DangerousGetHandle(), b.DangerousGetHandle());
        }

        [Theory]
        [InlineData(4, 2, 10)]
        [InlineData(5, 1, 6)]
        [InlineData(12, 4, 12)]
        public void RentReturnRent_SameBuffers(int totalCount, int rentUnit, int capacity)
        {
            var pool = new UniformUnmanagedMemoryPool(128, capacity);
            var allHandles = new HashSet<UnmanagedMemoryHandle>();
            var handleUnits = new List<UnmanagedMemoryHandle[]>();

            UnmanagedMemoryHandle[] handles;
            for (int i = 0; i < totalCount; i += rentUnit)
            {
                handles = pool.Rent(rentUnit);
                Assert.NotNull(handles);
                handleUnits.Add(handles);
                foreach (UnmanagedMemoryHandle array in handles)
                {
                    allHandles.Add(array);
                }
            }

            foreach (UnmanagedMemoryHandle[] arrayUnit in handleUnits)
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

            handles = pool.Rent(totalCount);

            Assert.NotNull(handles);

            foreach (UnmanagedMemoryHandle array in handles)
            {
                Assert.Contains(array, allHandles);
            }
        }

        [Fact]
        public void Rent_SingleBuffer_OverCapacity_ReturnsNull()
        {
            var pool = new UniformUnmanagedMemoryPool(7, 1000);
            Assert.NotNull(pool.Rent(1000));
            Assert.Null(pool.Rent());
        }

        [Theory]
        [InlineData(0, 6, 5)]
        [InlineData(5, 1, 5)]
        [InlineData(4, 7, 10)]
        public void Rent_MultiBuffer_OverCapacity_ReturnsNull(int initialRent, int attempt, int capacity)
        {
            var pool = new UniformUnmanagedMemoryPool(128, capacity);
            Assert.NotNull(pool.Rent(initialRent));
            Assert.Null(pool.Rent(attempt));
        }

        [Theory]
        [InlineData(0, 5, 5)]
        [InlineData(5, 1, 6)]
        [InlineData(4, 7, 11)]
        [InlineData(3, 3, 7)]
        public void Rent_MultiBuff_BelowCapacity_Succeeds(int initialRent, int attempt, int capacity)
        {
            var pool = new UniformUnmanagedMemoryPool(128, capacity);
            Assert.NotNull(pool.Rent(initialRent));
            Assert.NotNull(pool.Rent(attempt));
        }

        [Fact]
        public void RentReturn_IsThreadSafe()
        {
            int count = Environment.ProcessorCount * 200;
            var pool = new UniformUnmanagedMemoryPool(8, count);
            var rnd = new Random(0);

            Parallel.For(0, Environment.ProcessorCount, (int i) =>
            {
                var allArrays = new List<UnmanagedMemoryHandle>();
                int pauseAt = rnd.Next(100);
                for (int j = 0; j < 100; j++)
                {
                    UnmanagedMemoryHandle[] data = pool.Rent(2);

                    GetSpan(pool, data[0]).Fill((byte)i);
                    GetSpan(pool, data[1]).Fill((byte)i);
                    allArrays.Add(data[0]);
                    allArrays.Add(data[1]);

                    if (j == pauseAt)
                    {
                        Thread.Sleep(15);
                    }
                }

                Span<byte> expected = new byte[8];
                expected.Fill((byte)i);

                foreach (UnmanagedMemoryHandle array in allArrays)
                {
                    Assert.True(expected.SequenceEqual(GetSpan(pool, array)));
                    pool.Return(new[] { array });
                }
            });
        }
    }
}
