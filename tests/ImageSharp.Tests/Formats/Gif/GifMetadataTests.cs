// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;

using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    [Trait("Format", "Gif")]
    public class GifMetadataTests
    {
        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
            new TheoryData<string, int, int, PixelResolutionUnit>
            {
                { TestImages.Gif.Rings, (int)ImageMetadata.DefaultHorizontalResolution, (int)ImageMetadata.DefaultVerticalResolution, PixelResolutionUnit.PixelsPerInch },
                { TestImages.Gif.Ratio1x4, 1, 4, PixelResolutionUnit.AspectRatio },
                { TestImages.Gif.Ratio4x1, 4, 1, PixelResolutionUnit.AspectRatio }
            };

        public static readonly TheoryData<string, uint> RepeatFiles =
            new TheoryData<string, uint>
            {
                { TestImages.Gif.Cheers, 0 },
                { TestImages.Gif.Receipt, 1 },
                { TestImages.Gif.Rings, 1 }
            };

        [Fact]
        public void CloneIsDeep()
        {
            var meta = new GifMetadata
            {
                RepeatCount = 1,
                ColorTableMode = GifColorTableMode.Global,
                GlobalColorTableLength = 2,
                Comments = new List<string> { "Foo" }
            };

            var clone = (GifMetadata)meta.DeepClone();

            clone.RepeatCount = 2;
            clone.ColorTableMode = GifColorTableMode.Local;
            clone.GlobalColorTableLength = 1;

            Assert.False(meta.RepeatCount.Equals(clone.RepeatCount));
            Assert.False(meta.ColorTableMode.Equals(clone.ColorTableMode));
            Assert.False(meta.GlobalColorTableLength.Equals(clone.GlobalColorTableLength));
            Assert.False(meta.Comments.Equals(clone.Comments));
            Assert.True(meta.Comments.SequenceEqual(clone.Comments));
        }

        [Fact]
        public void Decode_IgnoreMetadataIsFalse_CommentsAreRead()
        {
            var options = new GifDecoder
            {
                IgnoreMetadata = false
            };

            var testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> image = testFile.CreateRgba32Image(options))
            {
                GifMetadata metadata = image.Metadata.GetGifMetadata();
                Assert.Equal(1, metadata.Comments.Count);
                Assert.Equal("ImageSharp", metadata.Comments[0]);
            }
        }

        [Fact]
        public void Decode_IgnoreMetadataIsTrue_CommentsAreIgnored()
        {
            var options = new GifDecoder
            {
                IgnoreMetadata = true
            };

            var testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> image = testFile.CreateRgba32Image(options))
            {
                GifMetadata metadata = image.Metadata.GetGifMetadata();
                Assert.Equal(0, metadata.Comments.Count);
            }
        }

        [Fact]
        public void Decode_CanDecodeLargeTextComment()
        {
            var options = new GifDecoder();
            var testFile = TestFile.Create(TestImages.Gif.LargeComment);

            using (Image<Rgba32> image = testFile.CreateRgba32Image(options))
            {
                GifMetadata metadata = image.Metadata.GetGifMetadata();
                Assert.Equal(2, metadata.Comments.Count);
                Assert.Equal(new string('c', 349), metadata.Comments[0]);
                Assert.Equal("ImageSharp", metadata.Comments[1]);
            }
        }

        [Fact]
        public void Encode_PreservesTextData()
        {
            var decoder = new GifDecoder();
            var testFile = TestFile.Create(TestImages.Gif.LargeComment);

            using (Image<Rgba32> input = testFile.CreateRgba32Image(decoder))
            using (var memoryStream = new MemoryStream())
            {
                input.Save(memoryStream, new GifEncoder());
                memoryStream.Position = 0;

                using (Image<Rgba32> image = decoder.Decode<Rgba32>(Configuration.Default, memoryStream))
                {
                    GifMetadata metadata = image.Metadata.GetGifMetadata();
                    Assert.Equal(2, metadata.Comments.Count);
                    Assert.Equal(new string('c', 349), metadata.Comments[0]);
                    Assert.Equal("ImageSharp", metadata.Comments[1]);
                }
            }
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Identify_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new GifDecoder();
                IImageInfo image = decoder.Identify(Configuration.Default, stream);
                ImageMetadata meta = image.Metadata;
                Assert.Equal(xResolution, meta.HorizontalResolution);
                Assert.Equal(yResolution, meta.VerticalResolution);
                Assert.Equal(resolutionUnit, meta.ResolutionUnits);
            }
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new GifDecoder();
                using (Image<Rgba32> image = decoder.Decode<Rgba32>(Configuration.Default, stream))
                {
                    ImageMetadata meta = image.Metadata;
                    Assert.Equal(xResolution, meta.HorizontalResolution);
                    Assert.Equal(yResolution, meta.VerticalResolution);
                    Assert.Equal(resolutionUnit, meta.ResolutionUnits);
                }
            }
        }

        [Theory]
        [MemberData(nameof(RepeatFiles))]
        public void Identify_VerifyRepeatCount(string imagePath, uint repeatCount)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new GifDecoder();
                IImageInfo image = decoder.Identify(Configuration.Default, stream);
                GifMetadata meta = image.Metadata.GetGifMetadata();
                Assert.Equal(repeatCount, meta.RepeatCount);
            }
        }

        [Theory]
        [MemberData(nameof(RepeatFiles))]
        public void Decode_VerifyRepeatCount(string imagePath, uint repeatCount)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new GifDecoder();
                using (Image<Rgba32> image = decoder.Decode<Rgba32>(Configuration.Default, stream))
                {
                    GifMetadata meta = image.Metadata.GetGifMetadata();
                    Assert.Equal(repeatCount, meta.RepeatCount);
                }
            }
        }
    }
}
