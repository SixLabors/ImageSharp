// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Moq;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class SaveAsync
    {
        [Fact]
        public async Task DetectedEncoding()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
            string file = Path.Combine(dir, "DetectedEncodingAsync.png");

            using (Image<Rgba32> image = new(10, 10))
            {
                await image.SaveAsync(file);
            }

            IImageFormat format = Image.DetectFormat(file);
            Assert.True(format is PngFormat);
        }

        [Fact]
        public Task WhenExtensionIsUnknown_Throws_UnknownImageFormatException()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
            string file = Path.Combine(dir, "UnknownExtensionsEncoding_Throws.tmp");

            return Assert.ThrowsAsync<UnknownImageFormatException>(
                async () =>
                    {
                        using Image<Rgba32> image = new(10, 10);
                        await image.SaveAsync(file);
                    });
        }

        [Fact]
        public async Task SetEncoding()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
            string file = Path.Combine(dir, "SetEncoding.dat");

            using (Image<Rgba32> image = new(10, 10))
            {
                await image.SaveAsync(file, new PngEncoder());
            }

            IImageFormat format = Image.DetectFormat(file);
            Assert.True(format is PngFormat);
        }

        [Theory]
        [InlineData("test.pbm", "image/x-portable-pixmap")]
        [InlineData("test.png", "image/png")]
        [InlineData("test.tga", "image/tga")]
        [InlineData("test.bmp", "image/bmp")]
        [InlineData("test.jpg", "image/jpeg")]
        [InlineData("test.gif", "image/gif")]
        public async Task SaveStreamWithMime(string filename, string mimeType)
        {
            using Image<Rgba32> image = new(5, 5);
            string ext = Path.GetExtension(filename);
            image.GetConfiguration().ImageFormatsManager.TryFindFormatByFileExtension(ext, out IImageFormat format);
            Assert.Equal(mimeType, format!.DefaultMimeType);

            using MemoryStream stream = new();
            AsyncStreamWrapper asyncStream = new(stream, () => false);
            await image.SaveAsync(asyncStream, format);

            stream.Position = 0;

            IImageFormat format2 = Image.DetectFormat(stream);
            Assert.Equal(format, format2);
        }

        [Fact]
        public async Task ThrowsWhenDisposed()
        {
            Image<Rgba32> image = new(5, 5);
            image.Dispose();
            IImageEncoder encoder = Mock.Of<IImageEncoder>();
            using MemoryStream stream = new();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await image.SaveAsync(stream, encoder));
        }

        [Theory]
        [InlineData("test.pbm")]
        [InlineData("test.png")]
        [InlineData("test.tga")]
        [InlineData("test.bmp")]
        [InlineData("test.jpg")]
        [InlineData("test.gif")]
        public async Task SaveAsync_NeverCallsSyncMethods(string filename)
        {
            using Image<Rgba32> image = new(5, 5);
            IImageEncoder encoder = image.DetectEncoder(filename);
            using MemoryStream stream = new();
            AsyncStreamWrapper asyncStream = new(stream, () => false);
            await image.SaveAsync(asyncStream, encoder);
        }

        [Fact]
        public async Task SaveAsync_WithNonSeekableStream_IsCancellable()
        {
            using Image<Rgba32> image = new(4000, 4000);
            PngEncoder encoder = new() { CompressionLevel = PngCompressionLevel.BestCompression };
            using MemoryStream stream = new();
            AsyncStreamWrapper asyncStream = new(stream, () => false);
            CancellationTokenSource cts = new();

            PausedStream pausedStream = new(asyncStream);
            pausedStream.OnWaiting(s =>
            {
                cts.Cancel();
                pausedStream.Release();
            });

            await Assert.ThrowsAsync<TaskCanceledException>(async () => await image.SaveAsync(pausedStream, encoder, cts.Token));
        }
    }
}
