// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers
{
    public partial class MemoryGroupTests
    {
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

            [Theory]
            [InlineData(30, 10, 40)]
            [InlineData(42, 23, 42)]
            [InlineData(1, 3, 10)]
            [InlineData(0, 4, 0)]
            public void GroupToSpan_Success(long totalLength, int bufferLength, int spanLength)
            {
                using MemoryGroup<int> src = this.CreateTestGroup(totalLength, bufferLength, true);
                var trg = new int[spanLength];
                src.CopyTo(trg);

                int expected = 1;
                foreach (int val in trg.AsSpan().Slice(0, (int)totalLength))
                {
                    Assert.Equal(expected, val);
                    expected++;
                }
            }

            [Theory]
            [InlineData(20, 7, 19)]
            [InlineData(2, 1, 1)]
            public void GroupToSpan_OutOfRange(long totalLength, int bufferLength, int spanLength)
            {
                using MemoryGroup<int> src = this.CreateTestGroup(totalLength, bufferLength, true);
                var trg = new int[spanLength];
                Assert.ThrowsAny<ArgumentOutOfRangeException>(() => src.CopyTo(trg));
            }

            [Theory]
            [InlineData(30, 35, 10)]
            [InlineData(42, 23, 42)]
            [InlineData(10, 3, 1)]
            [InlineData(0, 3, 0)]
            public void SpanToGroup_Success(long totalLength, int bufferLength, int spanLength)
            {
                var src = new int[spanLength];
                for (int i = 0; i < src.Length; i++)
                {
                    src[i] = i + 1;
                }

                using MemoryGroup<int> trg = this.CreateTestGroup(totalLength, bufferLength);
                src.AsSpan().CopyTo(trg);

                int position = 0;
                for (MemoryGroupIndex i = trg.MinIndex(); position < spanLength; i += 1, position++)
                {
                    int expected = position + 1;
                    Assert.Equal(expected, trg.GetElementAt(i));
                }
            }

            [Theory]
            [InlineData(10, 3, 11)]
            [InlineData(0, 3, 1)]
            public void SpanToGroup_OutOfRange(long totalLength, int bufferLength, int spanLength)
            {
                var src = new int[spanLength];
                using MemoryGroup<int> trg = this.CreateTestGroup(totalLength, bufferLength, true);
                Assert.ThrowsAny<ArgumentOutOfRangeException>(() => src.AsSpan().CopyTo(trg));
            }
        }
    }
}
