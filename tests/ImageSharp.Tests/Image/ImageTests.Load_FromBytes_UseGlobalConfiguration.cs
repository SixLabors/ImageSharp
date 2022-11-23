// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Load_FromBytes_UseGlobalConfiguration
    {
        private static byte[] ByteArray { get; } = TestFile.Create(TestImages.Bmp.Bit8).Bytes;

        private static Span<byte> ByteSpan => new(ByteArray);

        private static void VerifyDecodedImage(Image img) => Assert.Equal(new Size(127, 64), img.Size());

        [Fact]
        public void Bytes_Specific()
        {
            using var img = Image.Load<Rgba32>(ByteSpan);
            VerifyDecodedImage(img);
        }

        [Fact]
        public void Bytes_Agnostic()
        {
            using var img = Image.Load(ByteSpan);
            VerifyDecodedImage(img);
        }

        [Fact]
        public void Bytes_OutFormat_Specific()
        {
            using var img = Image.Load<Rgba32>(ByteSpan, out IImageFormat format);
            VerifyDecodedImage(img);
            Assert.IsType<BmpFormat>(format);
        }

        [Fact]
        public void Bytes_OutFormat_Agnostic()
        {
            using var img = Image.Load(ByteSpan, out IImageFormat format);
            VerifyDecodedImage(img);
            Assert.IsType<BmpFormat>(format);
        }
    }
}
