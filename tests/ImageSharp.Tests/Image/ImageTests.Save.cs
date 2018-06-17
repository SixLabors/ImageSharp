// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.Formats;

    public partial class ImageTests
    {
        public class Save
        {
            [Fact]
            public void DetecedEncoding()
            {
                string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
                string file = System.IO.Path.Combine(dir, "DetecedEncoding.png");

                using (var image = new Image<Rgba32>(10, 10))
                {
                    image.Save(file);
                }

                using (var img = Image.Load(file, out IImageFormat mime))
                {
                    Assert.Equal("image/png", mime.DefaultMimeType);
                }
            }

            [Fact]
            public void WhenExtensionIsUnknown_Throws()
            {
                string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
                string file = System.IO.Path.Combine(dir, "UnknownExtensionsEncoding_Throws.tmp");

                NotSupportedException ex = Assert.Throws<NotSupportedException>(
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

                using (var img = Image.Load(file, out var mime))
                {
                    Assert.Equal("image/png", mime.DefaultMimeType);
                }
            }
        }
    }
}