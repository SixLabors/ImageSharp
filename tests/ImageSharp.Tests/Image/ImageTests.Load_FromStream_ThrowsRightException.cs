// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Load_FromStream_Throws : IDisposable
    {
        private static readonly byte[] Data = new byte[] { 0x01 };

        private MemoryStream Stream { get; } = new(Data);

        [Fact]
        public void Image_Load_Throws_UnknownImageFormatException()
            => Assert.Throws<UnknownImageFormatException>(() =>
            {
                using (Image.Load(DecoderOptions.Default, this.Stream))
                {
                }
            });

        [Fact]
        public void Image_Load_T_Throws_UnknownImageFormatException()
            => Assert.Throws<UnknownImageFormatException>(() =>
            {
                using (Image.Load<Rgba32>(DecoderOptions.Default, this.Stream))
                {
                }
            });

        public void Dispose() => this.Stream?.Dispose();
    }
}
