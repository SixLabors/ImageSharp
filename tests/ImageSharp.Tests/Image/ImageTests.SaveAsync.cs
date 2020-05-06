// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.IO;

using Moq;

using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using SixLabors.ImageSharp.Advanced;
    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Tests.TestUtilities;

    public partial class ImageTests
    {
        public class SaveAsync
        {
            [Fact]
            public async Task DetectedEncoding()
            {
                string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
                string file = System.IO.Path.Combine(dir, "DetectedEncodingAsync.png");

                using (var image = new Image<Rgba32>(10, 10))
                {
                    await image.SaveAsync(file);
                }

                using (Image.Load(file, out IImageFormat mime))
                {
                    Assert.Equal("image/png", mime.DefaultMimeType);
                }
            }

            [Fact]
            public async Task WhenExtensionIsUnknown_Throws()
            {
                string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
                string file = System.IO.Path.Combine(dir, "UnknownExtensionsEncoding_Throws.tmp");

                await Assert.ThrowsAsync<NotSupportedException>(
                    async () =>
                        {
                            using (var image = new Image<Rgba32>(10, 10))
                            {
                                await image.SaveAsync(file);
                            }
                        });
            }

            [Fact]
            public async Task SetEncoding()
            {
                string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
                string file = System.IO.Path.Combine(dir, "SetEncoding.dat");

                using (var image = new Image<Rgba32>(10, 10))
                {
                    await image.SaveAsync(file, new PngEncoder());
                }

                using (Image.Load(file, out var mime))
                {
                    Assert.Equal("image/png", mime.DefaultMimeType);
                }
            }

            [Fact]
            public async Task ThrowsWhenDisposed()
            {
                var image = new Image<Rgba32>(5, 5);
                image.Dispose();
                IImageEncoder encoder = Mock.Of<IImageEncoder>();
                using (var stream = new MemoryStream())
                {
                    await Assert.ThrowsAsync<ObjectDisposedException>(async () => await image.SaveAsync(stream, encoder));
                }
            }

            [Theory]
            [InlineData("test.png")]
            [InlineData("test.tga")]
            [InlineData("test.bmp")]
            [InlineData("test.jpg")]
            [InlineData("test.gif")]
            public async Task SaveNeverCallsSyncMethods(string filename)
            {
                using (var image = new Image<Rgba32>(5, 5))
                {
                    IImageEncoder encoder = image.FindEncoded(filename);
                    using (var stream = new MemoryStream())
                    {
                        var asyncStream = new AsyncStreamWrapper(stream, () => false);
                        await image.SaveAsync(asyncStream, encoder);
                    }
                }
            }
        }
    }
}
