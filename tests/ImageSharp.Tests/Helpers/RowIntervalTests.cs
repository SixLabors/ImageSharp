using System;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class RowIntervalTests
    {
        [Theory]
        [InlineData(10, 20, 5, 10)]
        [InlineData(1, 10, 0, 10)]
        [InlineData(1, 10, 5, 8)]
        [InlineData(1, 1, 0, 1)]
        [InlineData(10, 20, 9, 10)]
        [InlineData(10, 20, 0, 1)]
        public void GetMultiRowSpan(int width, int height, int min, int max)
        {
            using (Buffer2D<int> buffer = Configuration.Default.MemoryAllocator.Allocate2D<int>(width, height))
            {
                var rows = new RowInterval(min, max);

                Span<int> span = buffer.GetMultiRowSpan(rows);
                
                ref int expected0 = ref buffer.Span[min * width];
                int expectedLength = (max - min) * width;

                ref int actual0 = ref span[0];

                Assert.Equal(span.Length, expectedLength);
                Assert.True(Unsafe.AreSame(ref expected0, ref actual0));
            }
        }
    }
}