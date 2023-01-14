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

            using (Image<Rgba32> image = new(10, 10))
            {
                image.Save(file);
            }

            Image.TryDetectFormat(file, out IImageFormat format);
            Assert.True(format is PngFormat);
        }

        [Fact]
        public void WhenExtensionIsUnknown_Throws_UnknownImageFormatException()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
            string file = Path.Combine(dir, "UnknownExtensionsEncoding_Throws.tmp");

            Assert.Throws<UnknownImageFormatException>(
                () =>
                    {
                        using Image<Rgba32> image = new(10, 10);
                        image.Save(file);
                    });
        }

        [Fact]
        public void SetEncoding()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
            string file = Path.Combine(dir, "SetEncoding.dat");

            using (Image<Rgba32> image = new(10, 10))
            {
                image.Save(file, new PngEncoder());
            }

            Image.TryDetectFormat(file, out IImageFormat format);
            Assert.True(format is PngFormat);
        }

        [Fact]
        public void ThrowsWhenDisposed()
        {
            using Image<Rgba32> image = new(5, 5);
            image.Dispose();
            IImageEncoder encoder = Mock.Of<IImageEncoder>();
            using MemoryStream stream = new();
            Assert.Throws<ObjectDisposedException>(() => image.Save(stream, encoder));
        }
    }
}
