// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using Moq;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class Load_FromBytes : ImageLoadTestBase
        {
            [Fact]
            public void BasicCase()
            {
                var img = Image.Load(this.TopLevelConfiguration, this.DataStream.ToArray());

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.Sample<Rgba32>(), img);

                this.TestFormat.VerifyDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void NonDefaultPixelType()
            {
                var img = Image.Load<Rgb24>(this.TopLevelConfiguration, this.DataStream.ToArray());

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.Sample<Rgb24>(), img);

                this.TestFormat.VerifyDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void UseLocalConfiguration()
            {
                var img = Image.Load<Rgba32>(this.LocalConfiguration, this.DataStream.ToArray());

                Assert.NotNull(img);

                this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, It.IsAny<Stream>()));

                Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
            }

            [Fact]
            public void UseCustomDecoder()
            {
                var img = Image.Load<Rgba32>(
                    this.TopLevelConfiguration,
                    this.DataStream.ToArray(),
                    this.localDecoder.Object);

                Assert.NotNull(img);
                this.localDecoder.Verify(x => x.Decode<Rgba32>(this.TopLevelConfiguration, It.IsAny<Stream>()));
                Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
            }
        }
    }
}