// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.MetaData;
    using static TestImages.Bmp;

    public class BmpDecoderTests
    {
        public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.RgbaVector;

        public static readonly string[] AllBmpFiles = All;

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new TheoryData<string, int, int, PixelResolutionUnit>
        {
            { TestImages.Bmp.Car, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { TestImages.Bmp.V5Header, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { TestImages.Bmp.RLE, 2835, 2835, PixelResolutionUnit.PixelsPerMeter }
        };

        [Theory]
        [WithFileCollection(nameof(AllBmpFiles), PixelTypes.Rgba32)]
        public void DecodeBmp<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider, "bmp");

                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider);
                }
            }
        }

        [Theory]
        [WithFile(F, CommonNonDefaultPixelTypes)]
        public void BmpDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage(new BmpDecoder()))
            {
                image.DebugSave(provider, "bmp");
                image.CompareToOriginal(provider);
            }
        }

        [Theory]
        [InlineData(Car, 24)]
        [InlineData(F, 24)]
        [InlineData(NegHeight, 24)]
        [InlineData(Bit8, 8)]
        [InlineData(Bit8Inverted, 8)]
        public void Identify(string imagePath, int expectedPixelSize)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                Assert.Equal(expectedPixelSize, Image.Identify(stream)?.PixelType?.BitsPerPixel);
            }
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Decode_VerifyRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var testFile = TestFile.Create(imagePath);
            using (var stream = new MemoryStream(testFile.Bytes, false))
            {
                var decoder = new BmpDecoder();
                using (Image<Rgba32> image = decoder.Decode<Rgba32>(Configuration.Default, stream))
                {
                    ImageMetaData meta = image.MetaData;
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
                var decoder = new BmpDecoder();
                IImageInfo image = decoder.Identify(Configuration.Default, stream);
                ImageMetaData meta = image.MetaData;
                Assert.Equal(xResolution, meta.HorizontalResolution);
                Assert.Equal(yResolution, meta.VerticalResolution);
                Assert.Equal(resolutionUnit, meta.ResolutionUnits);
            }
        }
    }
}