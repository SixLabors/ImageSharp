// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory
{
    using System;

    using SixLabors.ImageSharp.Memory;
    using SixLabors.Primitives;

    using Xunit;

    public class BufferAreaTests
    {
        [Fact]
        public void Construct()
        {
            using (var buffer = new Buffer2D<int>(10, 20))
            {
                var rectangle = new Rectangle(3,2, 5, 6);
                var area = new BufferArea<int>(buffer, rectangle);

                Assert.Equal(buffer, area.DestinationBuffer);
                Assert.Equal(rectangle, area.Rectangle);
            }
        }

        private static Buffer2D<int> CreateTestBuffer(int w, int h)
        {
            var buffer = new Buffer2D<int>(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    buffer[x, y] = y * 100 + x;
                }
            }
            return buffer;
        }
        
        [Theory]
        [InlineData(-1, 1, 0, 0)]
        [InlineData(1, -1, 0, 0)]
        [InlineData(0, 0, 1, 0)]
        [InlineData(0, 0, 0, 42)]
        public void Construct_WhenRectangleIsOutsideOfBufferBoundaries_Throws(int dx, int dy, int dWidth, int dHeight)
        {
            using (var buffer = new Buffer2D<int>(10, 20))
            {
                Rectangle r = buffer.FullRectangle();

                r = new Rectangle(r.X+dx, r.Y+dy, r.Width + dWidth, r.Height + dHeight );

                Assert.ThrowsAny<ArgumentException>(
                    () =>
                        {
                            var area = new BufferArea<int>(buffer, r);
                        });
            }
        }

        [Theory]
        [InlineData(2, 3, 2, 2)]
        [InlineData(5, 4, 3, 2)]
        public void Indexer(int rx, int ry, int x, int y)
        {
            using (Buffer2D<int> buffer = CreateTestBuffer(20, 30))
            {
                Rectangle r = new Rectangle(rx, ry, 5, 6);
                
                BufferArea<int> area = buffer.GetArea(r);

                int value = area[x, y];
                int expected = (ry + y) * 100 + rx + x;
                Assert.Equal(expected, value);
            }
        }

        [Theory]
        [InlineData(2, 3, 2, 5, 6)]
        [InlineData(5, 4, 3, 6, 5)]
        public void GetRowSpan(int rx, int ry, int y, int w, int h)
        {
            using (Buffer2D<int> buffer = CreateTestBuffer(20, 30))
            {
                Rectangle r = new Rectangle(rx, ry, w, h);

                BufferArea<int> area = buffer.GetArea(r);

                Span<int> span = area.GetRowSpan(y);

                Assert.Equal(w, span.Length);

                for (int i = 0; i < w; i++)
                {
                    int expected = (ry + y) * 100 + rx + i;
                    int value = span[i];

                    Assert.Equal(expected, value);
                }
            }
        }

        [Fact]
        public void GetSubArea()
        {
            using (Buffer2D<int> buffer = CreateTestBuffer(20, 30))
            {
                BufferArea<int> area0 = buffer.GetArea(6, 8, 10, 10);

                BufferArea<int> area1 = area0.GetSubArea(4, 4, 5, 5);

                var expectedRect = new Rectangle(10, 12, 5, 5);

                Assert.Equal(buffer, area1.DestinationBuffer);
                Assert.Equal(expectedRect, area1.Rectangle);

                int value00 = 12 * 100 + 10;
                Assert.Equal(value00, area1[0, 0]);
            }
        }

        [Fact]
        public void DangerousGetPinnableReference()
        {
            using (Buffer2D<int> buffer = CreateTestBuffer(20, 30))
            {
                BufferArea<int> area0 = buffer.GetArea(6, 8, 10, 10);

                ref int r = ref area0.GetReferenceToOrigo();

                int expected = buffer[6, 8];
                Assert.Equal(expected, r);
            }
        }
    }
}