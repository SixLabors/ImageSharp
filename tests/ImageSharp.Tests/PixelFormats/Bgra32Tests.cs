// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using ImageSharp.PixelFormats;

    using Xunit;

    public class Bgra32Tests
    {
        public static readonly TheoryData<byte, byte, byte, byte> ColorData =
            new TheoryData<byte, byte, byte, byte>()
                {
                    { 1, 2, 3, 4 }, { 4, 5, 6, 7 }, { 0, 255, 42, 0 }, { 1, 2, 3, 255 } 
                };

        [Theory]
        [MemberData(nameof(ColorData))]
        public void Constructor(byte b, byte g, byte r, byte a)
        {
            var p = new Bgra32(r, g, b, a);

            Assert.Equal(r, p.R);
            Assert.Equal(g, p.G);
            Assert.Equal(b, p.B);
            Assert.Equal(a, p.A);
        }

        [Fact]
        public unsafe void ByteLayoutIsSequentialBgra()
        {
            var color = new Bgra32(1, 2, 3, 4);
            byte* ptr = (byte*)&color;

            Assert.Equal(3, ptr[0]);
            Assert.Equal(2, ptr[1]);
            Assert.Equal(1, ptr[2]);
            Assert.Equal(4, ptr[3]);
        }

        [Theory]
        [MemberData(nameof(ColorData))]
        public void Equality_WhenTrue(byte b, byte g, byte r, byte a)
        {
            var x = new Bgra32(r, g, b, a);
            var y = new Bgra32(r, g, b, a);

            Assert.True(x.Equals(y));
            Assert.True(x.Equals((object)y));
            Assert.Equal(x.GetHashCode(), y.GetHashCode());
        }

        [Theory]
        [InlineData(1, 2, 3, 4, 1, 2, 3, 5)]
        [InlineData(0, 0, 255, 0, 0, 0, 244, 0)]
        [InlineData(0, 255, 0, 0, 0, 244, 0, 0)]
        [InlineData(1, 255, 0, 0, 0, 255, 0, 0)]
        public void Equality_WhenFalse(byte b1, byte g1, byte r1, byte a1, byte b2, byte g2, byte r2, byte a2)
        {
            var x = new Bgra32(r1, g1, b1, a1);
            var y = new Bgra32(r2, g2, b2, a2);

            Assert.False(x.Equals(y));
            Assert.False(x.Equals((object)y));
        }
    }
}