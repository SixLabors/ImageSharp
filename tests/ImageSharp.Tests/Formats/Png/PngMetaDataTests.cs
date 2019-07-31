// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    public class PngMetaDataTests
    {
        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
            new TheoryData<string, int, int, PixelResolutionUnit>
            {
                { TestImages.Png.Splash, 11810, 11810 , PixelResolutionUnit.PixelsPerMeter},
                { TestImages.Png.Ratio1x4, 1, 4 , PixelResolutionUnit.AspectRatio},
                { TestImages.Png.Ratio4x1, 4, 1, PixelResolutionUnit.AspectRatio }
            };

        [Fact]
        public void CloneIsDeep()
        {
            var meta = new PngMetadata()
            {
                BitDepth = PngBitDepth.Bit16,
                ColorType = PngColorType.GrayscaleWithAlpha,
                Gamma = 2
            };
            var clone = (PngMetadata)meta.DeepClone();

            clone.BitDepth = PngBitDepth.Bit2;
            clone.ColorType = PngColorType.Palette;
            clone.Gamma = 1;

            Assert.False(meta.BitDepth.Equals(clone.BitDepth));
            Assert.False(meta.ColorType.Equals(clone.ColorType));
            Assert.False(meta.Gamma.Equals(clone.Gamma));
        }

        [Theory]
        [WithFile(TestImages.Png.PngWithMetaData, PixelTypes.Rgba32)]
        public void Decoder_CanReadTextData<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                ImageMetadata meta = image.Metadata;
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("Comment") && m.Value.Equals("comment"));
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("Author") && m.Value.Equals("ImageSharp"));
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("Copyright") && m.Value.Equals("ImageSharp"));
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("Title") && m.Value.Equals("unittest"));
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("Description") && m.Value.Equals("compressed-text"));
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("International") && m.Value.Equals("'e', mu'tlheghvam, ghaH yu'") && m.LanguageTag.Equals("x-klingon") && m.TranslatedKeyword.Equals("warning"));
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("International2") && m.Value.Equals("ИМАГЕШАРП") && m.LanguageTag.Equals("rus"));
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("CompressedInternational") && m.Value.Equals("la plume de la mante") && m.LanguageTag.Equals("fra") && m.TranslatedKeyword.Equals("foobar"));
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("CompressedInternational2") && m.Value.Equals("這是一個考驗") && m.LanguageTag.Equals("chinese"));
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("NoLang") && m.Value.Equals("this text chunk is missing a language tag"));
                Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("NoTranslatedKeyword") && m.Value.Equals("dieser chunk hat kein übersetztes Schlüßelwort"));
            }
        }

        [Theory]
        [WithFile(TestImages.Png.PngWithMetaData, PixelTypes.Rgba32)]
        public void Encoder_PreservesTextData<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            var decoder = new PngDecoder();
            using (Image<TPixel> input = provider.GetImage(decoder))
            using (var memoryStream = new MemoryStream())
            {
                input.Save(memoryStream, new PngEncoder());

                memoryStream.Position = 0;
                using (Image<Rgba32> image = decoder.Decode<Rgba32>(Configuration.Default, memoryStream))
                {
                    ImageMetadata meta = image.Metadata;
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("Comment") && m.Value.Equals("comment"));
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("Author") && m.Value.Equals("ImageSharp"));
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("Copyright") && m.Value.Equals("ImageSharp"));
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("Title") && m.Value.Equals("unittest"));
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("Description") && m.Value.Equals("compressed-text"));
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("International") && m.Value.Equals("'e', mu'tlheghvam, ghaH yu'") && m.LanguageTag.Equals("x-klingon") && m.TranslatedKeyword.Equals("warning"));
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("International2") && m.Value.Equals("ИМАГЕШАРП") && m.LanguageTag.Equals("rus"));
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("CompressedInternational") && m.Value.Equals("la plume de la mante") && m.LanguageTag.Equals("fra") && m.TranslatedKeyword.Equals("foobar"));
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("CompressedInternational2") && m.Value.Equals("這是一個考驗") && m.LanguageTag.Equals("chinese"));
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("NoLang") && m.Value.Equals("this text chunk is missing a language tag"));
                    Assert.Contains(meta.PngTextProperties, m => m.Keyword.Equals("NoTranslatedKeyword") && m.Value.Equals("dieser chunk hat kein übersetztes Schlüßelwort"));
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Png.InvalidTextData, PixelTypes.Rgba32)]
        public void Decoder_IgnoresInvalidTextData<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new PngDecoder()))
            {
                ImageMetadata meta = image.Metadata;
                Assert.DoesNotContain(meta.PngTextProperties, m => m.Value.Equals("leading space"));
                Assert.DoesNotContain(meta.PngTextProperties, m => m.Value.Equals("trailing space"));
                Assert.DoesNotContain(meta.PngTextProperties, m => m.Value.Equals("space"));
                Assert.DoesNotContain(meta.PngTextProperties, m => m.Value.Equals("empty"));
                Assert.DoesNotContain(meta.PngTextProperties, m => m.Value.Equals("invalid characters"));
                Assert.DoesNotContain(meta.PngTextProperties, m => m.Value.Equals("too large"));
            }
        }

        [Theory]
        [WithFile(TestImages.Png.PngWithMetaData, PixelTypes.Rgba32)]
        public void Encode_UseCompression_WhenTextIsGreaterThenThreshold_Works<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            var decoder = new PngDecoder();
            using (Image<TPixel> input = provider.GetImage(decoder))
            using (var memoryStream = new MemoryStream())
            {
                // this will be a zTXt chunk.
                var expectedText = new PngTextData("large-text", new string('c', 100), string.Empty, string.Empty);
                // this will be a iTXt chunk.
                var expectedTextNoneLatin = new PngTextData("large-text-non-latin", new string('Ф', 100), "language-tag", "translated-keyword");
                input.Metadata.PngTextProperties.Add(expectedText);
                input.Metadata.PngTextProperties.Add(expectedTextNoneLatin);
                input.Save(memoryStream, new PngEncoder()
                                         {
                                            CompressTextThreshold = 50
                                         });

                memoryStream.Position = 0;
                using (Image<Rgba32> image = decoder.Decode<Rgba32>(Configuration.Default, memoryStream))
                {
                    ImageMetadata meta = image.Metadata;
                    Assert.Contains(meta.PngTextProperties, m => m.Equals(expectedText));
                    Assert.Contains(meta.PngTextProperties, m => m.Equals(expectedTextNoneLatin));
                }
            }
        }

        [Fact]
        public void Decode_IgnoreMetadataIsFalse_TextChunkIsRead()
        {
            var options = new PngDecoder()
                          {
                              IgnoreMetadata = false
                          };

            var testFile = TestFile.Create(TestImages.Png.Blur);

            using (Image<Rgba32> image = testFile.CreateRgba32Image(options))
            {
                PngMetadata formatMeta = image.Metadata.GetFormatMetadata(PngFormat.Instance);

                Assert.Equal(1, image.Metadata.PngTextProperties.Count);
                Assert.Equal(0, image.Metadata.GifTextProperties.Count);
                Assert.Equal("Software", image.Metadata.PngTextProperties[0].Keyword);
                Assert.Equal("paint.net 4.0.6", image.Metadata.PngTextProperties[0].Value);
                Assert.Equal(0.4545d, formatMeta.Gamma, precision: 4);
            }
        }

        [Fact]
        public void Decode_IgnoreMetadataIsTrue_TextChunksAreIgnored()
        {
            var options = new PngDecoder()
                          {
                              IgnoreMetadata = true
                          };

            var testFile = TestFile.Create(TestImages.Png.Blur);

            using (Image<Rgba32> image = testFile.CreateRgba32Image(options))
            {
                Assert.Equal(0, image.Metadata.PngTextProperties.Count);
                Assert.Equal(0, image.Metadata.GifTextProperties.Count);
            }
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new PngDecoder();
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
        [MemberData(nameof(RatioFiles))]
        public void Identify_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new PngDecoder();
                IImageInfo image = decoder.Identify(Configuration.Default, stream);
                ImageMetadata meta = image.Metadata;
                Assert.Equal(xResolution, meta.HorizontalResolution);
                Assert.Equal(yResolution, meta.VerticalResolution);
                Assert.Equal(resolutionUnit, meta.ResolutionUnits);
            }
        }
    }
}
