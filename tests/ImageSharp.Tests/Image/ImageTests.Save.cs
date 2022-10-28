// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Moq;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Save
    {
        [Fact]
        public void DetectedEncoding()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
            string file = Path.Combine(dir, "DetectedEncoding.png");

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
            string file = Path.Combine(dir, "UnknownExtensionsEncoding_Throws.tmp");

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
            string file = Path.Combine(dir, "SetEncoding.dat");

            using (var image = new Image<Rgba32>(10, 10))
            {
                image.Save(file, new PngEncoder());
            }

            using (Image.Load(file, out IImageFormat mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void ThrowsWhenDisposed()
        {
            using var image = new Image<Rgba32>(5, 5);
            image.Dispose();
            ImageEncoder encoder = Mock.Of<ImageEncoder>();
            using (var stream = new MemoryStream())
            {
                Assert.Throws<ObjectDisposedException>(() => image.Save(stream, encoder));
            }
        }
    }
}
