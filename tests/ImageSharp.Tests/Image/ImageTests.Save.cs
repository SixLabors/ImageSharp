// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Moq;
using SixLabors.ImageSharp.Advanced;
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

            IImageFormat format = Image.DetectFormat(file);
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

            IImageFormat format = Image.DetectFormat(file);
            Assert.True(format is PngFormat);
        }

        /// <summary>
        /// Checks that synchronous and asynchronous buffering preserve the bytes written by each encoder.
        /// </summary>
        /// <param name="filename">The filename used to select the encoder.</param>
        [Theory]
        [InlineData("test.ani")]
        [InlineData("test.bmp")]
        [InlineData("test.cur")]
        [InlineData("test.exr")]
        [InlineData("test.gif")]
        [InlineData("test.heif")]
        [InlineData("test.ico")]
        [InlineData("test.jpg")]
        [InlineData("test.pbm")]
        [InlineData("test.png")]
        [InlineData("test.qoi")]
        [InlineData("test.tga")]
        [InlineData("test.tiff")]
        [InlineData("test.webp")]
        public async Task ToNonSeekableStream_SyncAndAsync_MatchSeekableStream(string filename)
        {
            using Image<Rgba32> image = new(32, 24);
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        row[x] = new Rgba32((byte)(x * 8), (byte)(y * 10), (byte)((x + y) * 4));
                    }
                }
            });

            IImageEncoder encoder = image.DetectEncoder(filename);

            using MemoryStream seekable = new();
            image.Save(seekable, encoder);

            using MemoryStream syncTarget = new();
            image.Save(new NonSeekableStream(syncTarget), encoder);

            using MemoryStream asyncTarget = new();
            await image.SaveAsync(new NonSeekableStream(asyncTarget), encoder, TestContext.Current.CancellationToken);

            byte[] expected = seekable.ToArray();
            Assert.Equal(expected, syncTarget.ToArray());
            Assert.Equal(expected, asyncTarget.ToArray());
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
