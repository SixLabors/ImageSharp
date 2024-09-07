// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Load_FileSystemPath_UseDefaultConfiguration
    {
        private string Path { get; } = TestFile.GetInputFileFullPath(TestImages.Bmp.Bit8);

        private static void VerifyDecodedImage(Image img) => Assert.Equal(new(127, 64), img.Size);

        [Fact]
        public void Path_Specific()
        {
            using Image<Rgba32> img = Image.Load<Rgba32>(this.Path);
            VerifyDecodedImage(img);
        }

        [Fact]
        public void Path_Agnostic()
        {
            using Image img = Image.Load(this.Path);
            VerifyDecodedImage(img);
        }

        [Fact]
        public async Task Path_Agnostic_Async()
        {
            using Image img = await Image.LoadAsync(this.Path);
            VerifyDecodedImage(img);
        }

        [Fact]
        public async Task Path_Specific_Async()
        {
            using Image<Rgb24> img = await Image.LoadAsync<Rgb24>(this.Path);
            VerifyDecodedImage(img);
        }

        [Fact]
        public async Task Path_Agnostic_Configuration_Async()
        {
            using Image img = await Image.LoadAsync(this.Path);
            VerifyDecodedImage(img);
        }

        [Fact]
        public void Path_OutFormat_Specific()
        {
            using Image<Rgba32> img = Image.Load<Rgba32>(this.Path);
            VerifyDecodedImage(img);
            Assert.IsType<BmpFormat>(img.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void Path_OutFormat_Agnostic()
        {
            using Image img = Image.Load(this.Path);
            VerifyDecodedImage(img);
            Assert.IsType<BmpFormat>(img.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void WhenFileNotFound_Throws()
            => Assert.Throws<FileNotFoundException>(() => Image.Load<Rgba32>(Guid.NewGuid().ToString()));

        [Fact]
        public void WhenPathIsNull_Throws()
            => Assert.Throws<ArgumentNullException>(() => Image.Load<Rgba32>((string)null));

        [Fact]
        public Task Async_WhenFileNotFound_Throws()
            => Assert.ThrowsAsync<FileNotFoundException>(() => Image.LoadAsync<Rgba32>(Guid.NewGuid().ToString()));

        [Fact]
        public Task Async_WhenPathIsNull_Throws()
            => Assert.ThrowsAsync<ArgumentNullException>(() => Image.LoadAsync<Rgba32>((string)null));
    }
}
