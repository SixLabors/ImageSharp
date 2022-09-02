// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class Load_FromStream_PassLocalConfiguration : ImageLoadTestBase
        {
            [Fact]
            public void Configuration_Stream_Specific()
            {
                DecoderOptions options = new()
                {
                    Configuration = this.TopLevelConfiguration
                };

                var img = Image.Load<Rgb24>(options, this.DataStream);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.Sample<Rgb24>(), img);

                this.TestFormat.VerifySpecificDecodeCall<Rgb24>(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void Configuration_Stream_Agnostic()
            {
                DecoderOptions options = new()
                {
                    Configuration = this.TopLevelConfiguration
                };

                var img = Image.Load(options, this.DataStream);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.SampleAgnostic(), img);

                this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void NonSeekableStream()
            {
                DecoderOptions options = new()
                {
                    Configuration = this.TopLevelConfiguration
                };

                var stream = new NonSeekableStream(this.DataStream);
                var img = Image.Load<Rgba32>(options, stream);

                Assert.NotNull(img);

                this.TestFormat.VerifySpecificDecodeCall<Rgba32>(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public async Task NonSeekableStreamAsync()
            {
                DecoderOptions options = new()
                {
                    Configuration = this.TopLevelConfiguration
                };

                var stream = new NonSeekableStream(this.DataStream);
                Image<Rgba32> img = await Image.LoadAsync<Rgba32>(options, stream);

                Assert.NotNull(img);

                this.TestFormat.VerifySpecificDecodeCall<Rgba32>(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void Configuration_Stream_OutFormat_Specific()
            {
                DecoderOptions options = new()
                {
                    Configuration = this.TopLevelConfiguration
                };

                var img = Image.Load<Rgba32>(options, this.DataStream, out IImageFormat format);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat, format);

                this.TestFormat.VerifySpecificDecodeCall<Rgba32>(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void Configuration_Stream_OutFormat_Agnostic()
            {
                DecoderOptions options = new()
                {
                    Configuration = this.TopLevelConfiguration
                };

                var img = Image.Load(options, this.DataStream, out IImageFormat format);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat, format);

                this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
            }
        }
    }
}
