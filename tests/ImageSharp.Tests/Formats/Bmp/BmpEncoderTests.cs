// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    public class BmpEncoderTests : FileTestBase
    {
        public static readonly TheoryData<BmpBitsPerPixel> BitsPerPixel =
        new TheoryData<BmpBitsPerPixel>
        {
            BmpBitsPerPixel.Pixel24,
            BmpBitsPerPixel.Pixel32
        };

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new TheoryData<string, int, int, PixelResolutionUnit>
        {
            { TestImages.Bmp.Car, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { TestImages.Bmp.V5Header, 3780, 3780 , PixelResolutionUnit.PixelsPerMeter },
            { TestImages.Bmp.RLE, 2835, 2835, PixelResolutionUnit.PixelsPerMeter }
        };

        public static readonly TheoryData<string, BmpBitsPerPixel> BmpBitsPerPixelFiles =
        new TheoryData<string, BmpBitsPerPixel>
        {
            { TestImages.Bmp.Car, BmpBitsPerPixel.Pixel24 },
            { TestImages.Bmp.Bit32Rgb, BmpBitsPerPixel.Pixel32 }
        };

        public BmpEncoderTests(ITestOutputHelper output) => this.Output = output;

        private ITestOutputHelper Output { get; }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Encode_PreserveRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var options = new BmpEncoder();

            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateImage())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        ImageMetaData meta = output.MetaData;
                        Assert.Equal(xResolution, meta.HorizontalResolution);
                        Assert.Equal(yResolution, meta.VerticalResolution);
                        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(BmpBitsPerPixelFiles))]
        public void Encode_PreserveBitsPerPixel(string imagePath, BmpBitsPerPixel bmpBitsPerPixel)
        {
            var options = new BmpEncoder();

            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateImage())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        BmpMetaData meta = output.MetaData.GetFormatMetaData(BmpFormat.Instance);

                        Assert.Equal(bmpBitsPerPixel, meta.BitsPerPixel);
                    }
                }
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel), 24, 24, PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24)]
        public void Encode_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : struct, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel);

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel), 48, 24, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel), 47, 8, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel), 49, 7, PixelTypes.Rgba32)]
        [WithSolidFilledImages(nameof(BitsPerPixel), 1, 1, 255, 100, 50, 255, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel), 7, 5, PixelTypes.Rgba32)]
        public void Encode_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : struct, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel);

        private static void TestBmpEncoderCore<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                // there is no alpha in bmp!
                image.Mutate(c => c.MakeOpaque());

                var encoder = new BmpEncoder { BitsPerPixel = bitsPerPixel };

                // Does DebugSave & load reference CompareToReferenceInput():
                image.VerifyEncoder(provider, "bmp", bitsPerPixel, encoder);
            }
        }
    }
}