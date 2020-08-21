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
        public class Load_FileSystemPath_PassLocalConfiguration : ImageLoadTestBase
        {
            [Fact]
            public void Configuration_Path_Specific()
            {
                var img = Image.Load<Rgb24>(this.TopLevelConfiguration, this.MockFilePath);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.Sample<Rgb24>(), img);

                this.TestFormat.VerifySpecificDecodeCall<Rgb24>(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void Configuration_Path_Agnostic()
            {
                var img = Image.Load(this.TopLevelConfiguration, this.MockFilePath);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.SampleAgnostic(), img);

                this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void Configuration_Path_Decoder_Specific()
            {
                var img = Image.Load<Rgba32>(this.TopLevelConfiguration, this.MockFilePath, this.localDecoder.Object);

                Assert.NotNull(img);
                this.localDecoder.Verify(x => x.Decode<Rgba32>(this.TopLevelConfiguration, this.DataStream));
            }

            [Fact]
            public void Configuration_Path_Decoder_Agnostic()
            {
                var img = Image.Load(this.TopLevelConfiguration, this.MockFilePath, this.localDecoder.Object);

                Assert.NotNull(img);
                this.localDecoder.Verify(x => x.Decode(this.TopLevelConfiguration, this.DataStream));
            }

            [Fact]
            public void Configuration_Path_OutFormat_Specific()
            {
                var img = Image.Load<Rgba32>(this.TopLevelConfiguration, this.MockFilePath, out IImageFormat format);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat, format);

                this.TestFormat.VerifySpecificDecodeCall<Rgba32>(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void Configuration_Path_OutFormat_Agnostic()
            {
                var img = Image.Load(this.TopLevelConfiguration, this.MockFilePath, out IImageFormat format);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat, format);

                this.TestFormat.VerifyAgnosticDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void WhenFileNotFound_Throws()
            {
                Assert.Throws<System.IO.FileNotFoundException>(
                    () =>
                    {
                        Image.Load<Rgba32>(this.TopLevelConfiguration, Guid.NewGuid().ToString());
                    });
            }

            [Fact]
            public void WhenPathIsNull_Throws()
            {
                Assert.Throws<ArgumentNullException>(
                    () =>
                    {
                        Image.Load<Rgba32>(this.TopLevelConfiguration, (string)null);
                    });
            }
        }
    }
}
