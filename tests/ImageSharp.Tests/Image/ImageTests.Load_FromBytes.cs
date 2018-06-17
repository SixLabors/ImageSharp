// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using Moq;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class Load_FromBytes : ImageLoadTestBase
        {
            private byte[] ByteArray => this.DataStream.ToArray();

            private ReadOnlySpan<byte> ByteSpan => this.ByteArray.AsSpan();

            private byte[] ActualImageBytes => TestFile.Create(TestImages.Bmp.F).Bytes;

            private ReadOnlySpan<byte> ActualImageSpan => this.ActualImageBytes.AsSpan();

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void BasicCase(bool useSpan)
            {
                Image<Rgba32> img = useSpan
                                        ? Image.Load(this.TopLevelConfiguration, this.ByteSpan)
                                        : Image.Load(this.TopLevelConfiguration, this.ByteArray);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.Sample<Rgba32>(), img);

                this.TestFormat.VerifyDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void NonDefaultPixelType(bool useSpan)
            {
                Image<Rgb24> img = useSpan
                                       ? Image.Load<Rgb24>(this.TopLevelConfiguration, this.ByteSpan)
                                       : Image.Load<Rgb24>(this.TopLevelConfiguration, this.ByteArray);

                Assert.NotNull(img);
                Assert.Equal(this.TestFormat.Sample<Rgb24>(), img);

                this.TestFormat.VerifyDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void UseLocalConfiguration(bool useSpan)
            {
                Image<Rgba32> img = useSpan
                                        ? Image.Load<Rgba32>(this.LocalConfiguration, this.ByteSpan)
                                        : Image.Load<Rgba32>(this.LocalConfiguration, this.ByteArray);

                Assert.NotNull(img);

                this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, It.IsAny<Stream>()));

                Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void UseCustomDecoder(bool useSpan)
            {
                Image<Rgba32> img = useSpan
                                        ? Image.Load<Rgba32>(
                                            this.TopLevelConfiguration,
                                            this.ByteSpan,
                                            this.localDecoder.Object)
                                        : Image.Load<Rgba32>(
                                            this.TopLevelConfiguration,
                                            this.ByteArray,
                                            this.localDecoder.Object);
                Assert.NotNull(img);
                this.localDecoder.Verify(x => x.Decode<Rgba32>(this.TopLevelConfiguration, It.IsAny<Stream>()));
                Assert.Equal(this.DataStream.ToArray(), this.DecodedData);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void UseGlobalConfiguration(bool useSpan)
            {
                using (Image<Rgba32> img =
                    useSpan ? Image.Load(this.ActualImageSpan) : Image.Load(this.ActualImageBytes))
                {
                    Assert.Equal(new Size(108, 202), img.Size());
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void UseGlobalConfiguration_NonDefaultPixelType(bool useSpan)
            {
                using (Image<Rgb24> img = useSpan
                                              ? Image.Load<Rgb24>(this.ActualImageSpan)
                                              : Image.Load<Rgb24>(this.ActualImageBytes))
                {
                    Assert.Equal(new Size(108, 202), img.Size());
                }
            }
        }
    }
}