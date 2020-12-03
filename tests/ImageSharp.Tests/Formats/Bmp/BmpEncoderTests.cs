// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;
using Xunit.Abstractions;

using static SixLabors.ImageSharp.Tests.TestImages.Bmp;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Bmp
{
    [Trait("Format", "Bmp")]
    public class BmpEncoderTests
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
            { Car, 3780, 3780, PixelResolutionUnit.PixelsPerMeter },
            { V5Header, 3780, 3780, PixelResolutionUnit.PixelsPerMeter },
            { RLE8, 2835, 2835, PixelResolutionUnit.PixelsPerMeter }
        };

        public static readonly TheoryData<string, BmpBitsPerPixel> BmpBitsPerPixelFiles =
        new TheoryData<string, BmpBitsPerPixel>
        {
            { Car, BmpBitsPerPixel.Pixel24 },
            { Bit32Rgb, BmpBitsPerPixel.Pixel32 }
        };

        public BmpEncoderTests(ITestOutputHelper output) => this.Output = output;

        private ITestOutputHelper Output { get; }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Encode_PreserveRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var options = new BmpEncoder();

            var testFile = TestFile.Create(imagePath);
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        ImageMetadata meta = output.Metadata;
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
            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        BmpMetadata meta = output.Metadata.GetBmpMetadata();

                        Assert.Equal(bmpBitsPerPixel, meta.BitsPerPixel);
                    }
                }
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel), 24, 24, PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Rgb24)]
        public void Encode_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel);

        [Theory]
        [WithTestPatternImages(nameof(BitsPerPixel), 48, 24, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel), 47, 8, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel), 49, 7, PixelTypes.Rgba32)]
        [WithSolidFilledImages(nameof(BitsPerPixel), 1, 1, 255, 100, 50, 255, PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(BitsPerPixel), 7, 5, PixelTypes.Rgba32)]
        public void Encode_WorksWithDifferentSizes<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel);

        [Theory]
        [WithFile(Bit32Rgb, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Pixel32)]
        [WithFile(Bit32Rgba, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Pixel32)]
        [WithFile(WinBmpv4, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Pixel32)]
        [WithFile(WinBmpv5, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Pixel32)]
        public void Encode_32Bit_WithV3Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)

            // If supportTransparency is false, a v3 bitmap header will be written.
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: false);

        [Theory]
        [WithFile(Bit32Rgb, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Pixel32)]
        [WithFile(Bit32Rgba, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Pixel32)]
        [WithFile(WinBmpv4, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Pixel32)]
        [WithFile(WinBmpv5, PixelTypes.Rgba32 | PixelTypes.Rgb24, BmpBitsPerPixel.Pixel32)]
        public void Encode_32Bit_WithV4Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

        [Theory]
        [WithFile(WinBmpv3, PixelTypes.Rgb24, BmpBitsPerPixel.Pixel24)] // WinBmpv3 is a 24 bits per pixel image.
        [WithFile(F, PixelTypes.Rgb24, BmpBitsPerPixel.Pixel24)]
        public void Encode_24Bit_WithV3Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: false);

        [Theory]
        [WithFile(WinBmpv3, PixelTypes.Rgb24, BmpBitsPerPixel.Pixel24)]
        [WithFile(F, PixelTypes.Rgb24, BmpBitsPerPixel.Pixel24)]
        public void Encode_24Bit_WithV4Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

        [Theory]
        [WithFile(Rgb16, PixelTypes.Bgra5551, BmpBitsPerPixel.Pixel16)]
        [WithFile(Bit16, PixelTypes.Bgra5551, BmpBitsPerPixel.Pixel16)]
        public void Encode_16Bit_WithV3Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: false);

        [Theory]
        [WithFile(Rgb16, PixelTypes.Bgra5551, BmpBitsPerPixel.Pixel16)]
        [WithFile(Bit16, PixelTypes.Bgra5551, BmpBitsPerPixel.Pixel16)]
        public void Encode_16Bit_WithV4Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

        [Theory]
        [WithFile(WinBmpv5, PixelTypes.Rgba32, BmpBitsPerPixel.Pixel8)]
        [WithFile(Bit8Palette4, PixelTypes.Rgba32, BmpBitsPerPixel.Pixel8)]
        public void Encode_8Bit_WithV3Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: false);

        [Theory]
        [WithFile(WinBmpv5, PixelTypes.Rgba32, BmpBitsPerPixel.Pixel8)]
        [WithFile(Bit8Palette4, PixelTypes.Rgba32, BmpBitsPerPixel.Pixel8)]
        public void Encode_8Bit_WithV4Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

        [Theory]
        [WithFile(Bit8Gs, PixelTypes.L8, BmpBitsPerPixel.Pixel8)]
        public void Encode_8BitGray_WithV3Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestBmpEncoderCore(
                provider,
                bitsPerPixel,
                supportTransparency: false);

        [Theory]
        [WithFile(Bit8Gs, PixelTypes.L8, BmpBitsPerPixel.Pixel8)]
        public void Encode_8BitGray_WithV4Header_Works<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> =>
            TestBmpEncoderCore(
                provider,
                bitsPerPixel,
                supportTransparency: true);

        [Theory]
        [WithFile(Bit32Rgb, PixelTypes.Rgba32)]
        public void Encode_8BitColor_WithWuQuantizer<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                return;
            }

            using (Image<TPixel> image = provider.GetImage())
            {
                var encoder = new BmpEncoder
                {
                    BitsPerPixel = BmpBitsPerPixel.Pixel8,
                    Quantizer = new WuQuantizer()
                };
                string actualOutputFile = provider.Utility.SaveTestOutputFile(image, "bmp", encoder, appendPixelTypeToFileName: false);

                // Use the default decoder to test our encoded image. This verifies the content.
                // We do not verify the reference image though as some are invalid.
                IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(actualOutputFile);
                using (var referenceImage = Image.Load<TPixel>(actualOutputFile, referenceDecoder))
                {
                    referenceImage.CompareToReferenceOutput(
                        ImageComparer.TolerantPercentage(0.01f),
                        provider,
                        extension: "bmp",
                        appendPixelTypeToFileName: false,
                        decoder: new MagickReferenceDecoder(false));
                }
            }
        }

        [Theory]
        [WithFile(Bit32Rgb, PixelTypes.Rgba32)]
        public void Encode_8BitColor_WithOctreeQuantizer<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                return;
            }

            using (Image<TPixel> image = provider.GetImage())
            {
                var encoder = new BmpEncoder
                {
                    BitsPerPixel = BmpBitsPerPixel.Pixel8,
                    Quantizer = new OctreeQuantizer()
                };
                string actualOutputFile = provider.Utility.SaveTestOutputFile(image, "bmp", encoder, appendPixelTypeToFileName: false);

                // Use the default decoder to test our encoded image. This verifies the content.
                // We do not verify the reference image though as some are invalid.
                IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(actualOutputFile);
                using (var referenceImage = Image.Load<TPixel>(actualOutputFile, referenceDecoder))
                {
                    referenceImage.CompareToReferenceOutput(
                        ImageComparer.TolerantPercentage(0.01f),
                        provider,
                        extension: "bmp",
                        appendPixelTypeToFileName: false,
                        decoder: new MagickReferenceDecoder(false));
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Png.GrayAlpha2BitInterlaced, PixelTypes.Rgba32, BmpBitsPerPixel.Pixel32)]
        [WithFile(Bit32Rgba, PixelTypes.Rgba32, BmpBitsPerPixel.Pixel32)]
        public void Encode_PreservesAlpha<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel> => TestBmpEncoderCore(provider, bitsPerPixel, supportTransparency: true);

        [Theory]
        [WithFile(Car, PixelTypes.Rgba32, BmpBitsPerPixel.Pixel32)]
        [WithFile(V5Header, PixelTypes.Rgba32, BmpBitsPerPixel.Pixel32)]
        public void Encode_WorksWithDiscontiguousBuffers<TPixel>(TestImageProvider<TPixel> provider, BmpBitsPerPixel bitsPerPixel)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.LimitAllocatorBufferCapacity().InBytesSqrt(100);
            TestBmpEncoderCore(provider, bitsPerPixel);
        }

        private static void TestBmpEncoderCore<TPixel>(
            TestImageProvider<TPixel> provider,
            BmpBitsPerPixel bitsPerPixel,
            bool supportTransparency = true,
            ImageComparer customComparer = null)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                // There is no alpha in bmp with less then 32 bits per pixels, so the reference image will be made opaque.
                if (bitsPerPixel != BmpBitsPerPixel.Pixel32)
                {
                    image.Mutate(c => c.MakeOpaque());
                }

                var encoder = new BmpEncoder { BitsPerPixel = bitsPerPixel, SupportTransparency = supportTransparency };

                // Does DebugSave & load reference CompareToReferenceInput():
                image.VerifyEncoder(provider, "bmp", bitsPerPixel, encoder, customComparer);
            }
        }
    }
}
