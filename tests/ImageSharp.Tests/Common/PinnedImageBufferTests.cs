// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests.Common
{
    using System.Runtime.CompilerServices;

    using Xunit;

    public unsafe class PinnedImageBufferTests
    {
        [Theory]
        [InlineData(7, 42)]
        [InlineData(1025, 17)]
        public void Construct(int width, int height)
        {
            using (PinnedImageBuffer<int> buffer = new PinnedImageBuffer<int>(width, height))
            {
                Assert.Equal(width, buffer.Width);
                Assert.Equal(height, buffer.Height);
                Assert.Equal(width * height, buffer.Length);
            }
        }

        [Theory]
        [InlineData(7, 42)]
        [InlineData(1025, 17)]
        public void Construct_FromExternalArray(int width, int height)
        {
            int[] array = new int[width * height + 10];
            using (PinnedImageBuffer<int> buffer = new PinnedImageBuffer<int>(array, width, height))
            {
                Assert.Equal(width, buffer.Width);
                Assert.Equal(height, buffer.Height);
                Assert.Equal(width * height, buffer.Length);
            }
        }


        [Fact]
        public void CreateClean()
        {
            for (int i = 0; i < 100; i++)
            {
                using (PinnedImageBuffer<int> buffer = PinnedImageBuffer<int>.CreateClean(42, 42))
                {
                    for (int j = 0; j < buffer.Length; j++)
                    {
                        Assert.Equal(0, buffer.Array[j]);
                        buffer.Array[j] = 666;
                    }
                }
            }
        }

        [Theory]
        [InlineData(7, 42, 0)]
        [InlineData(7, 42, 10)]
        [InlineData(17, 42, 41)]
        public void GetRowSpanY(int width, int height, int y)
        {
            using (PinnedImageBuffer<int> buffer = new PinnedImageBuffer<int>(width, height))
            {
                BufferSpan<int> span = buffer.GetRowSpan(y);

                Assert.Equal(width * y, span.Start);
                Assert.Equal(width, span.Length);
                Assert.Equal(buffer.Pointer + sizeof(int) * width * y, span.PointerAtOffset);
            }
        }

        [Theory]
        [InlineData(7, 42, 0, 0)]
        [InlineData(7, 42, 3, 10)]
        [InlineData(17, 42, 0, 41)]
        public void GetRowSpanXY(int width, int height, int x, int y)
        {
            using (PinnedImageBuffer<int> buffer = new PinnedImageBuffer<int>(width, height))
            {
                BufferSpan<int> span = buffer.GetRowSpan(x, y);

                Assert.Equal(width * y + x, span.Start);
                Assert.Equal(width - x, span.Length);
                Assert.Equal(buffer.Pointer + sizeof(int) * (width * y + x), span.PointerAtOffset);
            }
        }
    }
}