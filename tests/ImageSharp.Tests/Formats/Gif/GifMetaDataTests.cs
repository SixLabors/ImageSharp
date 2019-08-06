// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Text;

using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    public class GifMetaDataTests
    {
        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
            new TheoryData<string, int, int, PixelResolutionUnit>
            {
                { TestImages.Gif.Rings, (int)ImageMetadata.DefaultHorizontalResolution, (int)ImageMetadata.DefaultVerticalResolution , PixelResolutionUnit.PixelsPerInch},
                { TestImages.Gif.Ratio1x4, 1, 4 , PixelResolutionUnit.AspectRatio},
                { TestImages.Gif.Ratio4x1, 4, 1, PixelResolutionUnit.AspectRatio }
            };

        [Fact]
        public void CloneIsDeep()
        {
            var meta = new GifMetadata()
            {
                RepeatCount = 1,
                ColorTableMode = GifColorTableMode.Global,
                GlobalColorTableLength = 2
            };

            var clone = (GifMetadata)meta.DeepClone();

            clone.RepeatCount = 2;
            clone.ColorTableMode = GifColorTableMode.Local;
            clone.GlobalColorTableLength = 1;

            Assert.False(meta.RepeatCount.Equals(clone.RepeatCount));
            Assert.False(meta.ColorTableMode.Equals(clone.ColorTableMode));
            Assert.False(meta.GlobalColorTableLength.Equals(clone.GlobalColorTableLength));
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
                Assert.Equal(1, image.Metadata.GifComments.Count);
                Assert.Equal("ImageSharp", image.Metadata.GifComments[0]);
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
                Assert.Equal(0, image.Metadata.GifComments.Count);
            }
        }

        [Fact]
        public void Decode_TextEncodingSetToUnicode_TextIsReadWithCorrectEncoding()
        {
            var options = new GifDecoder
                          {
                              TextEncoding = Encoding.Unicode
                          };

            var testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> image = testFile.CreateRgba32Image(options))
            {
                Assert.Equal(1, image.Metadata.GifComments.Count);
                Assert.Equal("浉条卥慨灲", image.Metadata.GifComments[0]);
            }
        }

        [Fact]
        public void Decode_CanDecodeLargeTextComment()
        {
            var options = new GifDecoder();
            var testFile = TestFile.Create(TestImages.Gif.LargeComment);

            using (Image<Rgba32> image = testFile.CreateRgba32Image(options))
            {
                Assert.Equal(2, image.Metadata.GifComments.Count);
                Assert.Equal(new string('c', 349), image.Metadata.GifComments[0]);
                Assert.Equal("ImageSharp", image.Metadata.GifComments[1]);
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
                    Assert.Equal(2, image.Metadata.GifComments.Count);
                    Assert.Equal(new string('c', 349), image.Metadata.GifComments[0]);
                    Assert.Equal("ImageSharp", image.Metadata.GifComments[1]);
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
    }
}
