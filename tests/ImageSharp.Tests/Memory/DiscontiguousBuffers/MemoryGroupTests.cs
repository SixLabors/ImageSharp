// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers
{
    public partial class MemoryGroupTests : MemoryGroupTestsBase
    {
        [Fact]
        public void IsValid_TrueAfterCreation()
        {
            using var g = MemoryGroup<byte>.Allocate(this.MemoryAllocator, 10, 100);

            Assert.True(g.IsValid);
        }

        [Fact]
        public void IsValid_FalseAfterDisposal()
        {
            using var g = MemoryGroup<byte>.Allocate(this.MemoryAllocator, 10, 100);

            g.Dispose();
            Assert.False(g.IsValid);
        }

#pragma warning disable SA1509
        private static readonly TheoryData<int, int, int, int> CopyAndTransformData =
            new TheoryData<int, int, int, int>()
            {
                { 20, 10, 20, 10 },
                { 20, 5, 20, 4 },
                { 20, 4, 20, 5 },
                { 18, 6, 20, 5 },
                { 19, 10, 20, 10 },
                { 21, 10, 22, 2 },
                { 1, 5, 5, 4 },

                { 30, 12, 40, 5 },
                { 30, 5, 40, 12 },
            };

        public class TransformTo : MemoryGroupTestsBase
        {
            public static readonly TheoryData<int, int, int, int> WhenSourceBufferIsShorterOrEqual_Data =
                CopyAndTransformData;

            [Theory]
            [MemberData(nameof(WhenSourceBufferIsShorterOrEqual_Data))]
            public void WhenSourceBufferIsShorterOrEqual(int srcTotal, int srcBufLen, int trgTotal, int trgBufLen)
            {
                using MemoryGroup<int> src = this.CreateTestGroup(srcTotal, srcBufLen, true);
                using MemoryGroup<int> trg = this.CreateTestGroup(trgTotal, trgBufLen, false);

                src.TransformTo(trg, MultiplyAllBy2);

                int pos = 0;
                MemoryGroupIndex i = src.MinIndex();
                MemoryGroupIndex j = trg.MinIndex();
                for (; i < src.MaxIndex(); i += 1, j += 1, pos++)
                {
                    int a = src.GetElementAt(i);
                    int b = trg.GetElementAt(j);

                    Assert.True(b == 2 * a, $"Mismatch @ {pos} Expected: {a} Actual: {b}");
                }
            }

            [Fact]
            public void WhenTargetBufferTooShort_Throws()
            {
                using MemoryGroup<int> src = this.CreateTestGroup(10, 20, true);
                using MemoryGroup<int> trg = this.CreateTestGroup(5, 20, false);

                Assert.Throws<ArgumentOutOfRangeException>(() => src.TransformTo(trg, MultiplyAllBy2));
            }
        }

        [Theory]
        [InlineData(100, 5)]
        [InlineData(100, 101)]
        public void TransformInplace(int totalLength, int bufferLength)
        {
            using MemoryGroup<int> src = this.CreateTestGroup(10, 20, true);

            src.TransformInplace(s => MultiplyAllBy2(s, s));

            int cnt = 1;
            for (MemoryGroupIndex i = src.MinIndex(); i < src.MaxIndex(); i += 1)
            {
                int val = src.GetElementAt(i);
                Assert.Equal(expected: cnt * 2, val);
                cnt++;
            }
        }

        [Fact]
        public void Wrap()
        {
            int[] data0 = { 1, 2, 3, 4 };
            int[] data1 = { 5, 6, 7, 8 };
            int[] data2 = { 9, 10 };
            using var mgr0 = new TestMemoryManager<int>(data0);
            using var mgr1 = new TestMemoryManager<int>(data1);

            using var group = MemoryGroup<int>.Wrap(mgr0.Memory, mgr1.Memory, data2);

            Assert.Equal(3, group.Count);
            Assert.Equal(4, group.BufferLength);
            Assert.Equal(10, group.TotalLength);

            Assert.True(group[0].Span.SequenceEqual(data0));
            Assert.True(group[1].Span.SequenceEqual(data1));
            Assert.True(group[2].Span.SequenceEqual(data2));
        }

        public static TheoryData<long, int, long, int> GetBoundedSlice_SuccessData = new TheoryData<long, int, long, int>()
        {
            { 300, 100, 110, 80 },
            { 300, 100, 100, 100 },
            { 280, 100, 201, 79 },
            { 42, 7, 0, 0 },
            { 42, 7, 0, 1 },
            { 42, 7, 0, 7 },
            { 42, 9, 9, 9 },
        };

        [Theory]
        [MemberData(nameof(GetBoundedSlice_SuccessData))]
        public void GetBoundedSlice_WhenArgsAreCorrect(long totalLength, int bufferLength, long start, int length)
        {
            using MemoryGroup<int> group = this.CreateTestGroup(totalLength, bufferLength, true);

            Memory<int> slice = group.GetBoundedSlice(start, length);

            Assert.Equal(length, slice.Length);

            int expected = (int)start + 1;
            foreach (int val in slice.Span)
            {
                Assert.Equal(expected, val);
                expected++;
            }
        }

        public static TheoryData<long, int, long, int> GetBoundedSlice_ErrorData = new TheoryData<long, int, long, int>()
        {
            { 300, 100, -1, 91 },
            { 300, 100, 110, 91 },
            { 42, 7, 0, 8 },
            { 42, 7, 1, 7 },
            { 42, 7, 1, 30 },
        };

        [Theory]
        [MemberData(nameof(GetBoundedSlice_ErrorData))]
        public void GetBoundedSlice_WhenOverlapsBuffers_Throws(long totalLength, int bufferLength, long start, int length)
        {
            using MemoryGroup<int> group = this.CreateTestGroup(totalLength, bufferLength, true);
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => group.GetBoundedSlice(start, length));
        }

        [Fact]
        public void FillWithFastEnumerator()
        {
            using MemoryGroup<int> group = this.CreateTestGroup(100, 10, true);
            group.Fill(42);

            int[] expectedRow = Enumerable.Repeat(42, 10).ToArray();
            foreach (Memory<int> memory in group)
            {
                Assert.True(memory.Span.SequenceEqual(expectedRow));
            }
        }

        [Fact]
        public void FillWithSlowGenericEnumerator()
        {
            using MemoryGroup<int> group = this.CreateTestGroup(100, 10, true);
            group.Fill(42);

            int[] expectedRow = Enumerable.Repeat(42, 10).ToArray();
            IReadOnlyList<Memory<int>> groupAsList = group;
            foreach (Memory<int> memory in groupAsList)
            {
                Assert.True(memory.Span.SequenceEqual(expectedRow));
            }
        }

        [Fact]
        public void FillWithSlowEnumerator()
        {
            using MemoryGroup<int> group = this.CreateTestGroup(100, 10, true);
            group.Fill(42);

            int[] expectedRow = Enumerable.Repeat(42, 10).ToArray();
            IEnumerable groupAsList = group;
            foreach (Memory<int> memory in groupAsList)
            {
                Assert.True(memory.Span.SequenceEqual(expectedRow));
            }
        }

        [Fact]
        public void Clear()
        {
            using MemoryGroup<int> group = this.CreateTestGroup(100, 10, true);
            group.Clear();

            var expectedRow = new int[10];
            foreach (Memory<int> memory in group)
            {
                Assert.True(memory.Span.SequenceEqual(expectedRow));
            }
        }

        private static void MultiplyAllBy2(ReadOnlySpan<int> source, Span<int> target)
        {
            Assert.Equal(source.Length, target.Length);
            for (int k = 0; k < source.Length; k++)
            {
                target[k] = source[k] * 2;
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 5)]
        private struct S5
        {
            public override string ToString() => "S5";
        }

        [StructLayout(LayoutKind.Sequential, Size = 4)]
        private struct S4
        {
            public override string ToString() => "S4";
        }
    }
}
