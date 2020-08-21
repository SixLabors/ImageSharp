// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
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
                var img = Image.Load<Rgb24>(this.TopLevelConfiguration, this.DataStream);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.Sample<Rgb24>(), img);

                this.TestFormat.VerifySpecificDecodeCall<Rgb24>(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void Configuration_Stream_Agnostic()
            {
                var img = Image.Load(this.TopLevelConfiguration, this.DataStream);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.SampleAgnostic(), img);

                this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void NonSeekableStream()
            {
                var stream = new NonSeekableStream(this.DataStream);
                var img = Image.Load<Rgba32>(this.TopLevelConfiguration, stream);

                Assert.NotNull(img);

                this.TestFormat.VerifySpecificDecodeCall<Rgba32>(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public async Task NonSeekableStreamAsync()
            {
                var stream = new NonSeekableStream(this.DataStream);
                Image<Rgba32> img = await Image.LoadAsync<Rgba32>(this.TopLevelConfiguration, stream);

                Assert.NotNull(img);

                this.TestFormat.VerifySpecificDecodeCall<Rgba32>(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void Configuration_Stream_Decoder_Specific()
            {
                var stream = new MemoryStream();
                var img = Image.Load<Rgba32>(this.TopLevelConfiguration, stream, this.localDecoder.Object);

                Assert.NotNull(img);
                this.localDecoder.Verify(x => x.Decode<Rgba32>(this.TopLevelConfiguration, stream));
            }

            [Fact]
            public void Configuration_Stream_Decoder_Agnostic()
            {
                var stream = new MemoryStream();
                var img = Image.Load(this.TopLevelConfiguration, stream, this.localDecoder.Object);

                Assert.NotNull(img);
                this.localDecoder.Verify(x => x.Decode(this.TopLevelConfiguration, stream));
            }

            [Fact]
            public void Configuration_Stream_OutFormat_Specific()
            {
                var img = Image.Load<Rgba32>(this.TopLevelConfiguration, this.DataStream, out IImageFormat format);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat, format);

                this.TestFormat.VerifySpecificDecodeCall<Rgba32>(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void Configuration_Stream_OutFormat_Agnostic()
            {
                var img = Image.Load(this.TopLevelConfiguration, this.DataStream, out IImageFormat format);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat, format);

                this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
            }
        }
    }
}
