// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public partial class ImageTests
    {
        public class Constructor
        {
            [Fact]
            public void Width_Height()
            {
                using (var image = new Image<Rgba32>(11, 23))
                {
                    Assert.Equal(11, image.Width);
                    Assert.Equal(23, image.Height);
                    Assert.Equal(11*23, image.GetPixelSpan().Length);
                    image.ComparePixelBufferTo(default(Rgba32));

                    Assert.Equal(Configuration.Default, image.GetConfiguration());
                }
            }

            [Fact]
            public void Configuration_Width_Height()
            {
                Configuration configuration = Configuration.Default.Clone();

                using (var image = new Image<Rgba32>(configuration, 11, 23))
                {
                    Assert.Equal(11, image.Width);
                    Assert.Equal(23, image.Height);
                    Assert.Equal(11 * 23, image.GetPixelSpan().Length);
                    image.ComparePixelBufferTo(default(Rgba32));

                    Assert.Equal(configuration, image.GetConfiguration());
                }
            }

            [Fact]
            public void Configuration_Width_Height_BackroundColor()
            {
                Configuration configuration = Configuration.Default.Clone();
                Rgba32 color = Rgba32.Aquamarine;

                using (var image = new Image<Rgba32>(configuration, 11, 23, color))
                {
                    Assert.Equal(11, image.Width);
                    Assert.Equal(23, image.Height);
                    Assert.Equal(11 * 23, image.GetPixelSpan().Length);
                    image.ComparePixelBufferTo(color);

                    Assert.Equal(configuration, image.GetConfiguration());
                }
            }
        }
    }
}
