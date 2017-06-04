// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using ImageSharp.PixelFormats;

    using Xunit;

    public class Bgr24Tests
    {
        public static readonly TheoryData<byte, byte, byte> ColorData =
            new TheoryData<byte, byte, byte>() { { 1, 2, 3 }, { 4, 5, 6 }, { 0, 255, 42 } };

        [Theory]
        [MemberData(nameof(ColorData))]
        public void Constructor(byte r, byte g, byte b)
        {
            var p = new Rgb24(r, g, b);

            Assert.Equal(r, p.R);
            Assert.Equal(g, p.G);
            Assert.Equal(b, p.B);
        }
        
        [Fact]
        public unsafe void ByteLayoutIsSequentialBgr()
        {
            var color = new Bgr24(1, 2, 3);
            byte* ptr = (byte*)&color;
        
            Assert.Equal(3, ptr[0]);
            Assert.Equal(2, ptr[1]);
            Assert.Equal(1, ptr[2]);
        }

        [Theory]
        [MemberData(nameof(ColorData))]
        public void Equals_WhenTrue(byte r, byte g, byte b)
        {
            var x = new Rgb24(r, g, b);
            var y = new Rgb24(r, g, b);

            Assert.True(x.Equals(y));
            Assert.True(x.Equals((object)y));
            Assert.Equal(x.GetHashCode(), y.GetHashCode());
        }

        [Theory]
        [InlineData(1, 2, 3, 1, 2, 4)]
        [InlineData(0, 255, 0, 0, 244, 0)]
        [InlineData(1, 255, 0, 0, 255, 0)]
        public void Equals_WhenFalse(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            var a = new Rgb24(r1, g1, b1);
            var b = new Rgb24(r2, g2, b2);

            Assert.False(a.Equals(b));
            Assert.False(a.Equals((object)b));
        }
    }
}