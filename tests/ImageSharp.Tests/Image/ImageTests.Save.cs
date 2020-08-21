// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using Moq;

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.Formats;

    public partial class ImageTests
    {
        public class Save
        {
            [Fact]
            public void DetectedEncoding()
            {
                string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
                string file = System.IO.Path.Combine(dir, "DetectedEncoding.png");

                using (var image = new Image<Rgba32>(10, 10))
                {
                    image.Save(file);
                }

                using (Image.Load(file, out IImageFormat mime))
                {
                    Assert.Equal("image/png", mime.DefaultMimeType);
                }
            }

            [Fact]
            public void WhenExtensionIsUnknown_Throws()
            {
                string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
                string file = System.IO.Path.Combine(dir, "UnknownExtensionsEncoding_Throws.tmp");

                Assert.Throws<NotSupportedException>(
                    () =>
                        {
                            using (var image = new Image<Rgba32>(10, 10))
                            {
                                image.Save(file);
                            }
                        });
            }

            [Fact]
            public void SetEncoding()
            {
                string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
                string file = System.IO.Path.Combine(dir, "SetEncoding.dat");

                using (var image = new Image<Rgba32>(10, 10))
                {
                    image.Save(file, new PngEncoder());
                }

                using (Image.Load(file, out var mime))
                {
                    Assert.Equal("image/png", mime.DefaultMimeType);
                }
            }

            [Fact]
            public void ThrowsWhenDisposed()
            {
                var image = new Image<Rgba32>(5, 5);
                image.Dispose();
                IImageEncoder encoder = Mock.Of<IImageEncoder>();
                using (var stream = new MemoryStream())
                {
                    Assert.Throws<ObjectDisposedException>(() => image.Save(stream, encoder));
                }
            }
        }
    }
}
