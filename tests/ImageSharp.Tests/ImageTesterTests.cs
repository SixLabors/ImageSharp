// ReSharper disable InconsistentNaming

namespace ImageSharp.Tests
{
    using System;
    using System.IO;

    using Xunit;

    public class ImageTesterTests
    {
        [Fact]
        public void GetPackedType()
        {
            Type shouldBeUIint32 = ImageTester.GetPackedType(typeof(Color));

            Assert.Equal(shouldBeUIint32, typeof(uint));
        }

        public static readonly TheoryData<Type> TestPixelTypes = new TheoryData<Type>()
                                                                     {
                                                                         typeof(Color),
                                                                         typeof(HalfVector4),
                                                                         typeof(Rgba1010102)
                                                                     };

        [Theory]
        [MemberData(nameof(TestPixelTypes))]
        public void Create(Type pixelType)
        {
            var tester = ImageTester.Create(pixelType, 42, 66);

            Assert.Equal(tester.Image.Width, 42);
            Assert.Equal(tester.Image.Height, 66);
        }

        [Theory]
        [MemberData(nameof(TestPixelTypes))]
        public void Create_FromStream(Type pixelType)
        {
            var image = new TestFile(TestImages.Bmp.Car).CreateImage();

            using (var stream = File.OpenRead(TestImages.Bmp.Car))
            {
                var tester = ImageTester.Create(pixelType, stream);

                Assert.Equal(tester.Image.Width, image.Width);
                Assert.Equal(tester.Image.Height, image.Height);
            }
        }

        [Fact]
        public void GetPixelAsVector4()
        {
            var tester = ImageTester.Create(typeof(Color), TestImages.Bmp.RGB3x3);

        }

        [Fact]
        public void SetPixelFromVector4()
        {

        }
    }
}