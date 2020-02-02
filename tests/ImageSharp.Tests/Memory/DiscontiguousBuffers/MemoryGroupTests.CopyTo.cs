// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers
{
    public partial class MemoryGroupTests
    {
        public class CopyTo : MemoryGroupTestsBase
        {
#pragma warning disable SA1509
            public static readonly TheoryData<int, int, int, int> WhenSourceBufferIsShorterOrEqual_Data =
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

            [Theory]
            [MemberData(nameof(WhenSourceBufferIsShorterOrEqual_Data))]
            public void WhenSourceBufferIsShorterOrEqual(int srcTotal, int srcBufLen, int trgTotal, int trgBufLen)
            {
                using MemoryGroup<int> src = this.CreateTestGroup(srcTotal, srcBufLen, true);
                using MemoryGroup<int> trg = this.CreateTestGroup(trgTotal, trgBufLen, false);

                src.CopyTo(trg);

                MemoryGroupIndex i = src.MinIndex();
                MemoryGroupIndex j = trg.MinIndex();
                for (; i < src.MaxIndex(); i += 1, j += 1)
                {
                    int a = src.GetElementAt(i);
                    int b = src.GetElementAt(j);

                    Assert.Equal(a, b);
                }
            }
        }
    }
}
