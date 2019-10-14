// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    using SixLabors.ImageSharp.Formats.WebP;
    using SixLabors.ImageSharp.Metadata;
    using static TestImages.Bmp;

    public class WebPDecoderTests
    {
        public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.RgbaVector;

        public static readonly string[] MiscBmpFiles = Miscellaneous;

        public static readonly string[] BitfieldsBmpFiles = BitFields;

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new TheoryData<string, int, int, PixelResolutionUnit>
        {
            { Car, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { V5Header, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { RLE8, 2835, 2835, PixelResolutionUnit.PixelsPerMeter }
        };

        [Theory]
        [WithFileCollection(nameof(MiscBmpFiles), PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_MiscellaneousBitmaps<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new WebPDecoder()))
            {
                image.DebugSave(provider);
                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider);
                }
            }
        }

        [Theory]
        [WithFile(Bit16Inverted, PixelTypes.Rgba32)]
        [WithFile(Bit8Inverted, PixelTypes.Rgba32)]
        public void BmpDecoder_CanDecode_Inverted<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new WebPDecoder()))
            {
                image.DebugSave(provider);
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [InlineData(Bit32Rgb, 32)]
        [InlineData(Bit32Rgba, 32)]
        [InlineData(Car, 24)]
        [InlineData(F, 24)]
        [InlineData(NegHeight, 24)]
        [InlineData(Bit16, 16)]
        [InlineData(Bit16Inverted, 16)]
        [InlineData(Bit8, 8)]
        [InlineData(Bit8Inverted, 8)]
        [InlineData(Bit4, 4)]
        [InlineData(Bit1, 1)]
        [InlineData(Bit1Pal1, 1)]
        public void Identify_DetectsCorrectPixelType(string imagePath, int expectedPixelSize)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                IImageInfo imageInfo = Image.Identify(stream);
                Assert.NotNull(imageInfo);
                Assert.Equal(expectedPixelSize, imageInfo.PixelType?.BitsPerPixel);
            }
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new WebPDecoder();
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
