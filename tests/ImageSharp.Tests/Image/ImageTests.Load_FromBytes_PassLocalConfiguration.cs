// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Load_FromBytes_PassLocalConfiguration : ImageLoadTestBase
    {
        private ReadOnlySpan<byte> ByteSpan => this.ByteArray.AsSpan();

        [Fact]
        public void Configuration_Bytes_Specific()
        {
            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            using (Image<Rgb24> img = Image.Load<Rgb24>(options, this.ByteSpan))
            {
                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.Sample<Rgb24>(), img);
            }

            this.TestFormat.VerifySpecificDecodeCall<Rgb24>(this.Marker, this.TopLevelConfiguration);
        }

        [Fact]
        public void Configuration_Bytes_Agnostic()
        {
            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            using (Image img = Image.Load(options, this.ByteSpan))
            {
                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.SampleAgnostic(), img);
            }

            this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
        }

        [Fact]
        public void Configuration_Bytes_OutFormat_Specific()
        {
            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            using (Image<Bgr24> img = Image.Load<Bgr24>(options, this.ByteSpan))
            {
                Assert.NotNull(img);
                Assert.Equal(this.TestFormat, img.Metadata.DecodedImageFormat);
            }

            this.TestFormat.VerifySpecificDecodeCall<Bgr24>(this.Marker, this.TopLevelConfiguration);
        }

        [Fact]
        public void Configuration_Bytes_OutFormat_Agnostic()
        {
            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            using (Image img = Image.Load(options, this.ByteSpan))
            {
                Assert.NotNull(img);
                Assert.Equal(this.TestFormat, img.Metadata.DecodedImageFormat);
            }

            this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
        }

        [Fact]
        public void FromBytes_EmptySpan_Throws()
        {
            DecoderOptions options = new()
            {
                Configuration = this.TopLevelConfiguration
            };

            Assert.Throws<UnknownImageFormatException>(() => Image.Load(options, []));
        }
    }
}
