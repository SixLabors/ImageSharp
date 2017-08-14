using SixLabors.Primitives;
using Xunit;

namespace ImageSharp.Tests
{
    public class ImageRotationTests
    {
        [Fact]
        public void RotateImageByMinus90Degrees()
        {
            (Size original, Size rotated) = Rotate(-90);
            Assert.Equal(new Size(original.Height, original.Width), rotated);
        }

        [Fact]
        public void RotateImageBy90Degrees()
        {
            (Size original, Size rotated) = Rotate(90);
            Assert.Equal(new Size(original.Height, original.Width), rotated);
        }

        [Fact]
        public void RotateImageBy180Degrees()
        {
            (Size original, Size rotated) = Rotate(180);
            Assert.Equal(original, rotated);
        }

        [Fact]
        public void RotateImageBy270Degrees()
        {
            (Size original, Size rotated) = Rotate(270);
            Assert.Equal(new Size(original.Height, original.Width), rotated);
        }

        [Fact]
        public void RotateImageBy360Degrees()
        {
            (Size original, Size rotated) = Rotate(360);
            Assert.Equal(original, rotated);
        }

        private static (Size original, Size rotated) Rotate(int angle)
        {
            TestFile file = TestFile.Create(TestImages.Bmp.Car);
            using (Image<Rgba32> image = Image.Load<Rgba32>(file.FilePath))
            {
                Size original = image.Bounds().Size;
                image.Mutate(ctx => ctx.Rotate(angle));
                return (original, image.Bounds().Size);
            }
        }
    }
}
