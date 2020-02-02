// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
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

        public class CopyTo : MemoryGroupTestsBase
        {
            public static readonly TheoryData<int, int, int, int> WhenSourceBufferIsShorterOrEqual_Data =
                CopyAndTransformData;

            [Theory]
            [MemberData(nameof(WhenSourceBufferIsShorterOrEqual_Data))]
            public void WhenSourceBufferIsShorterOrEqual(int srcTotal, int srcBufLen, int trgTotal, int trgBufLen)
            {
                using MemoryGroup<int> src = this.CreateTestGroup(srcTotal, srcBufLen, true);
                using MemoryGroup<int> trg = this.CreateTestGroup(trgTotal, trgBufLen, false);

                src.CopyTo(trg);

                int pos = 0;
                MemoryGroupIndex i = src.MinIndex();
                MemoryGroupIndex j = trg.MinIndex();
                for (; i < src.MaxIndex(); i += 1, j += 1, pos++)
                {
                    int a = src.GetElementAt(i);
                    int b = trg.GetElementAt(j);

                    Assert.True(a == b, $"Mismatch @ {pos} Expected: {a} Actual: {b}");
                }
            }

            [Fact]
            public void WhenTargetBufferTooShort_Throws()
            {
                using MemoryGroup<int> src = this.CreateTestGroup(10, 20, true);
                using MemoryGroup<int> trg = this.CreateTestGroup(5, 20, false);

                Assert.Throws<ArgumentOutOfRangeException>(() => src.CopyTo(trg));
            }
        }

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
