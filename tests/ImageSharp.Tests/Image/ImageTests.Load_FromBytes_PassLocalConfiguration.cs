// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class Load_FromBytes_PassLocalConfiguration : ImageLoadTestBase
        {
            private ReadOnlySpan<byte> ByteSpan => this.ByteArray.AsSpan();

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Configuration_Bytes_Specific(bool useSpan)
            {
                var img = useSpan
                              ? Image.Load<Rgb24>(this.TopLevelConfiguration, this.ByteSpan)
                              : Image.Load<Rgb24>(this.TopLevelConfiguration, this.ByteArray);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.Sample<Rgb24>(), img);

                this.TestFormat.VerifySpecificDecodeCall<Rgb24>(this.Marker, this.TopLevelConfiguration);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Configuration_Bytes_Agnostic(bool useSpan)
            {
                var img = useSpan
                              ? Image.Load(this.TopLevelConfiguration, this.ByteSpan)
                              : Image.Load(this.TopLevelConfiguration, this.ByteArray);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.SampleAgnostic(), img);

                this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Configuration_Bytes_Decoder_Specific(bool useSpan)
            {
                var localFormat = new TestFormat();

                var img = useSpan ?
                              Image.Load<Rgba32>(this.TopLevelConfiguration, this.ByteSpan, localFormat.Decoder) :
                              Image.Load<Rgba32>(this.TopLevelConfiguration, this.ByteArray, localFormat.Decoder);

                Assert.NotNull(img);
                localFormat.VerifySpecificDecodeCall<Rgba32>(this.Marker, this.TopLevelConfiguration);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Configuration_Bytes_Decoder_Agnostic(bool useSpan)
            {
                var localFormat = new TestFormat();

                var img = useSpan ?
                              Image.Load(this.TopLevelConfiguration, this.ByteSpan, localFormat.Decoder) :
                              Image.Load(this.TopLevelConfiguration, this.ByteArray, localFormat.Decoder);

                Assert.NotNull(img);
                localFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Configuration_Bytes_OutFormat_Specific(bool useSpan)
            {
                IImageFormat format;
                var img = useSpan ?
                              Image.Load<Bgr24>(this.TopLevelConfiguration, this.ByteSpan, out format) :
                              Image.Load<Bgr24>(this.TopLevelConfiguration, this.ByteArray, out format);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat, format);

                this.TestFormat.VerifySpecificDecodeCall<Bgr24>(this.Marker, this.TopLevelConfiguration);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Configuration_Bytes_OutFormat_Agnostic(bool useSpan)
            {
                IImageFormat format;
                var img = useSpan ?
                              Image.Load(this.TopLevelConfiguration, this.ByteSpan, out format) :
                              Image.Load(this.TopLevelConfiguration, this.ByteArray, out format);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat, format);

                this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
            }
        }
    }
}
