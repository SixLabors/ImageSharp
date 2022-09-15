// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Load_FileSystemPath_PassLocalConfiguration : ImageLoadTestBase
    {
        [Fact]
        public void Configuration_Path_Specific()
        {
            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            var img = Image.Load<Rgb24>(options, this.MockFilePath);

            Assert.NotNull(img);
            Assert.Equal(this.TestFormat.Sample<Rgb24>(), img);

            this.TestFormat.VerifySpecificDecodeCall<Rgb24>(this.Marker, this.TopLevelConfiguration);
        }

        [Fact]
        public void Configuration_Path_Agnostic()
        {
            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            var img = Image.Load(options, this.MockFilePath);

            Assert.NotNull(img);
            Assert.Equal(this.TestFormat.SampleAgnostic(), img);

            this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
        }

        [Fact]
        public void Configuration_Path_OutFormat_Specific()
        {
            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            var img = Image.Load<Rgba32>(options, this.MockFilePath, out IImageFormat format);

            Assert.NotNull(img);
            Assert.Equal(this.TestFormat, format);

            this.TestFormat.VerifySpecificDecodeCall<Rgba32>(this.Marker, this.TopLevelConfiguration);
        }

        [Fact]
        public void Configuration_Path_OutFormat_Agnostic()
        {
            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            var img = Image.Load(options, this.MockFilePath, out IImageFormat format);

            Assert.NotNull(img);
            Assert.Equal(this.TestFormat, format);

            this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
        }

        [Fact]
        public void WhenFileNotFound_Throws()
            => Assert.Throws<System.IO.FileNotFoundException>(
                () =>
                {
                    DecoderOptions options = new()
                    {
                        Configuration = this.TopLevelConfiguration
                    };

                    Image.Load<Rgba32>(options, Guid.NewGuid().ToString());
                });

        [Fact]
        public void WhenPathIsNull_Throws()
            => Assert.Throws<ArgumentNullException>(
                () =>
                {
                    DecoderOptions options = new()
                    {
                        Configuration = this.TopLevelConfiguration
                    };
                    Image.Load<Rgba32>(options, (string)null);
                });
    }
}
