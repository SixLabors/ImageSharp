// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Load_FromStream_UseDefaultConfiguration : IDisposable
    {
        private static readonly byte[] Data = TestFile.Create(TestImages.Bmp.Bit8).Bytes;

        private MemoryStream BaseStream { get; }

        private AsyncStreamWrapper Stream { get; }

        private bool AllowSynchronousIO { get; set; } = true;

        public Load_FromStream_UseDefaultConfiguration()
        {
            this.BaseStream = new(Data);
            this.Stream = new(this.BaseStream, () => this.AllowSynchronousIO);
        }

        private static void VerifyDecodedImage(Image img)
            => Assert.Equal(new(127, 64), img.Size);

        [Fact]
        public void Stream_Specific()
        {
            using Image<Rgba32> img = Image.Load<Rgba32>(this.Stream);
            VerifyDecodedImage(img);
        }

        [Fact]
        public void Stream_Agnostic()
        {
            using Image img = Image.Load(this.Stream);
            VerifyDecodedImage(img);
        }

        [Fact]
        public void Stream_OutFormat_Specific()
        {
            using Image<Rgba32> img = Image.Load<Rgba32>(this.Stream);
            VerifyDecodedImage(img);
            Assert.IsType<BmpFormat>(img.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void Stream_OutFormat_Agnostic()
        {
            using Image img = Image.Load(this.Stream);
            VerifyDecodedImage(img);
            Assert.IsType<BmpFormat>(img.Metadata.DecodedImageFormat);
        }

        [Fact]
        public async Task Async_Stream_OutFormat_Agnostic()
        {
            this.AllowSynchronousIO = false;
            Image image = await Image.LoadAsync(this.Stream);
            using (image)
            {
                VerifyDecodedImage(image);
                Assert.IsType<BmpFormat>(image.Metadata.DecodedImageFormat);
            }
        }

        [Fact]
        public async Task Async_Stream_Specific()
        {
            this.AllowSynchronousIO = false;
            using Image<Rgba32> img = await Image.LoadAsync<Rgba32>(this.Stream);
            VerifyDecodedImage(img);
        }

        [Fact]
        public async Task Async_Stream_Agnostic()
        {
            this.AllowSynchronousIO = false;
            using Image img = await Image.LoadAsync(this.Stream);
            VerifyDecodedImage(img);
        }

        [Fact]
        public async Task Async_Stream_OutFormat_Specific()
        {
            this.AllowSynchronousIO = false;
            Image<Rgba32> image = await Image.LoadAsync<Rgba32>(this.Stream);
            using (image)
            {
                VerifyDecodedImage(image);
                Assert.IsType<BmpFormat>(image.Metadata.DecodedImageFormat);
            }
        }

        public void Dispose() => this.BaseStream?.Dispose();
    }
}
