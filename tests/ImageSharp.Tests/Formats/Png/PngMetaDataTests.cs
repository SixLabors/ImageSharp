// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Text;
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
                Assert.Contains(meta.Properties, m => m.Name.Equals("Comment") && m.Value.Equals("comment"));
                Assert.Contains(meta.Properties, m => m.Name.Equals("Author") && m.Value.Equals("ImageSharp"));
                Assert.Contains(meta.Properties, m => m.Name.Equals("Copyright") && m.Value.Equals("ImageSharp"));
                Assert.Contains(meta.Properties, m => m.Name.Equals("Title") && m.Value.Equals("unittest"));
                Assert.Contains(meta.Properties, m => m.Name.Equals("Description") && m.Value.Equals("compressed-text"));
                Assert.Contains(meta.Properties, m => m.Name.Equals("International") && m.Value.Equals("'e', mu'tlheghvam, ghaH yu'"));
                Assert.Contains(meta.Properties, m => m.Name.Equals("CompressedInternational") && m.Value.Equals("la plume de la mante"));
                Assert.Contains(meta.Properties, m => m.Name.Equals("CompressedInternational2") && m.Value.Equals("這是一個考驗"));
                Assert.Contains(meta.Properties, m => m.Name.Equals("NoLang") && m.Value.Equals("this text chunk is missing a language tag"));
                Assert.Contains(meta.Properties, m => m.Name.Equals("NoTranslatedKeyword") && m.Value.Equals("dieser chunk hat kein übersetztes Schlüßelwort"));
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
                    Assert.Contains(meta.Properties, m => m.Name.Equals("Comment") && m.Value.Equals("comment"));
                    Assert.Contains(meta.Properties, m => m.Name.Equals("Author") && m.Value.Equals("ImageSharp"));
                    Assert.Contains(meta.Properties, m => m.Name.Equals("Copyright") && m.Value.Equals("ImageSharp"));
                    Assert.Contains(meta.Properties, m => m.Name.Equals("Title") && m.Value.Equals("unittest"));
                    Assert.Contains(meta.Properties, m => m.Name.Equals("Description") && m.Value.Equals("compressed-text"));
                    Assert.Contains(meta.Properties, m => m.Name.Equals("International") && m.Value.Equals("'e', mu'tlheghvam, ghaH yu'"));
                    Assert.Contains(meta.Properties, m => m.Name.Equals("CompressedInternational") && m.Value.Equals("la plume de la mante"));
                    Assert.Contains(meta.Properties, m => m.Name.Equals("CompressedInternational2") && m.Value.Equals("這是一個考驗"));
                    Assert.Contains(meta.Properties, m => m.Name.Equals("NoLang") && m.Value.Equals("this text chunk is missing a language tag"));
                    Assert.Contains(meta.Properties, m => m.Name.Equals("NoTranslatedKeyword") && m.Value.Equals("dieser chunk hat kein übersetztes Schlüßelwort"));
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
                Assert.DoesNotContain(meta.Properties, m => m.Value.Equals("leading space"));
                Assert.DoesNotContain(meta.Properties, m => m.Value.Equals("trailing space"));
                Assert.DoesNotContain(meta.Properties, m => m.Value.Equals("space"));
                Assert.DoesNotContain(meta.Properties, m => m.Value.Equals("empty"));
                Assert.DoesNotContain(meta.Properties, m => m.Value.Equals("invalid characters"));
                Assert.DoesNotContain(meta.Properties, m => m.Value.Equals("too large"));
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

                Assert.Equal(1, image.Metadata.Properties.Count);
                Assert.Equal("Software", image.Metadata.Properties[0].Name);
                Assert.Equal("paint.net 4.0.6", image.Metadata.Properties[0].Value);
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
                Assert.Equal(0, image.Metadata.Properties.Count);
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
