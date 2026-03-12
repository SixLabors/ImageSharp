// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ColorTests
{
    public class CastFrom
    {
        [Fact]
        public void Rgba64()
        {
            Rgba64 source = new(100, 2222, 3333, 4444);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Rgba64 data = color.ToPixel<Rgba64>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Rgba32()
        {
            Rgba32 source = new(1, 22, 33, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Rgba32 data = color.ToPixel<Rgba32>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Argb32()
        {
            Argb32 source = new(1, 22, 33, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Argb32 data = color.ToPixel<Argb32>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Bgra32()
        {
            Bgra32 source = new(1, 22, 33, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Bgra32 data = color.ToPixel<Bgra32>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Abgr32()
        {
            Abgr32 source = new(1, 22, 33, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Abgr32 data = color.ToPixel<Abgr32>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Rgb24()
        {
            Rgb24 source = new(1, 22, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Rgb24 data = color.ToPixel<Rgb24>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Bgr24()
        {
            Bgr24 source = new(1, 22, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Bgr24 data = color.ToPixel<Bgr24>();
            Assert.Equal(source, data);
        }
    }
}
