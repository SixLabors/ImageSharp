// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ColorTests
    {
        public class ConstructFrom
        {
            [Fact]
            public void Rgba64()
            {
                var source = new Rgba64(100, 2222, 3333, 4444);

                // Act:
                var color = new Color(source);

                // Assert:
                Rgba64 data = color.ToPixel<Rgba64>();
                Assert.Equal(source, data);
            }

            [Fact]
            public void Rgba32()
            {
                var source = new Rgba32(1, 22, 33, 231);

                // Act:
                var color = new Color(source);

                // Assert:
                Rgba32 data = color.ToPixel<Rgba32>();
                Assert.Equal(source, data);
            }

            [Fact]
            public void Argb32()
            {
                var source = new Argb32(1, 22, 33, 231);

                // Act:
                var color = new Color(source);

                // Assert:
                Argb32 data = color.ToPixel<Argb32>();
                Assert.Equal(source, data);
            }

            [Fact]
            public void Bgra32()
            {
                var source = new Bgra32(1, 22, 33, 231);

                // Act:
                var color = new Color(source);

                // Assert:
                Bgra32 data = color.ToPixel<Bgra32>();
                Assert.Equal(source, data);
            }

            [Fact]
            public void Rgb24()
            {
                var source = new Rgb24(1, 22,  231);

                // Act:
                var color = new Color(source);

                // Assert:
                Rgb24 data = color.ToPixel<Rgb24>();
                Assert.Equal(source, data);
            }

            [Fact]
            public void Bgr24()
            {
                var source = new Bgr24(1, 22,  231);

                // Act:
                var color = new Color(source);

                // Assert:
                Bgr24 data = color.ToPixel<Bgr24>();
                Assert.Equal(source, data);
            }
        }
    }
}
