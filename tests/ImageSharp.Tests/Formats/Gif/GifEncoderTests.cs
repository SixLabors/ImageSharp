// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    [Trait("Format", "Gif")]
    public class GifEncoderTests
    {
        private const PixelTypes TestPixelTypes = PixelTypes.Rgba32 | PixelTypes.RgbaVector | PixelTypes.Argb32;
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.0015F);

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new TheoryData<string, int, int, PixelResolutionUnit>
        {
            { TestImages.Gif.Rings, (int)ImageMetadata.DefaultHorizontalResolution, (int)ImageMetadata.DefaultVerticalResolution, PixelResolutionUnit.PixelsPerInch },
            { TestImages.Gif.Ratio1x4, 1, 4, PixelResolutionUnit.AspectRatio },
            { TestImages.Gif.Ratio4x1, 4, 1, PixelResolutionUnit.AspectRatio }
        };

        [Theory]
        [WithTestPatternImages(100, 100, TestPixelTypes, false)]
        [WithTestPatternImages(100, 100, TestPixelTypes, false)]
        public void EncodeGeneratedPatterns<TPixel>(TestImageProvider<TPixel> provider, bool limitAllocationBuffer)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (limitAllocationBuffer)
            {
                provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);
            }

            using (Image<TPixel> image = provider.GetImage())
            {
                var encoder = new GifEncoder
                {
                    // Use the palette quantizer without dithering to ensure results
                    // are consistent
                    Quantizer = new WebSafePaletteQuantizer(new QuantizerOptions { Dither = null })
                };

                // Always save as we need to compare the encoded output.
                provider.Utility.SaveTestOutputFile(image, "gif", encoder);
            }

            // Compare encoded result
            string path = provider.Utility.GetTestOutputFileName("gif", null, true);
            using (var encoded = Image.Load<Rgba32>(path))
            {
                encoded.CompareToReferenceOutput(ValidatorComparer, provider, null, "gif");
            }
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Encode_PreserveRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var options = new GifEncoder();

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

        [Fact]
        public void Encode_IgnoreMetadataIsFalse_CommentsAreWritten()
        {
            var options = new GifEncoder();

            var testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> input = testFile.CreateRgba32Image())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        GifMetadata metadata = output.Metadata.GetGifMetadata();
                        Assert.Equal(1, metadata.Comments.Count);
                        Assert.Equal("ImageSharp", metadata.Comments[0]);
                    }
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Cheers, PixelTypes.Rgba32)]
        public void EncodeGlobalPaletteReturnsSmallerFile<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var encoder = new GifEncoder
                {
                    ColorTableMode = GifColorTableMode.Global,
                    Quantizer = new OctreeQuantizer(new QuantizerOptions { Dither = null })
                };

                // Always save as we need to compare the encoded output.
                provider.Utility.SaveTestOutputFile(image, "gif", encoder, "global");

                encoder.ColorTableMode = GifColorTableMode.Local;
                provider.Utility.SaveTestOutputFile(image, "gif", encoder, "local");

                var fileInfoGlobal = new FileInfo(provider.Utility.GetTestOutputFileName("gif", "global"));
                var fileInfoLocal = new FileInfo(provider.Utility.GetTestOutputFileName("gif", "local"));

                Assert.True(fileInfoGlobal.Length < fileInfoLocal.Length);
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.GlobalQuantizationTest, PixelTypes.Rgba32, 427500, 0.1)]
        [WithFile(TestImages.Gif.GlobalQuantizationTest, PixelTypes.Rgba32, 200000, 0.1)]
        [WithFile(TestImages.Gif.GlobalQuantizationTest, PixelTypes.Rgba32, 100000, 0.1)]
        [WithFile(TestImages.Gif.GlobalQuantizationTest, PixelTypes.Rgba32, 50000, 0.1)]
        [WithFile(TestImages.Gif.Cheers, PixelTypes.Rgba32, 4000000, 0.01)]
        [WithFile(TestImages.Gif.Cheers, PixelTypes.Rgba32, 1000000, 0.01)]
        public void Encode_GlobalPalette_DefaultPixelSamplingStrategy<TPixel>(TestImageProvider<TPixel> provider, int maxPixels, double scanRatio)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using Image<TPixel> image = provider.GetImage();

            var encoder = new GifEncoder()
            {
                ColorTableMode = GifColorTableMode.Global,
                GlobalPixelSamplingStrategy = new DefaultPixelSamplingStrategy(maxPixels, scanRatio)
            };

            string testOutputFile = provider.Utility.SaveTestOutputFile(
                image,
                "gif",
                encoder,
                testOutputDetails: $"{maxPixels}_{scanRatio}",
                appendPixelTypeToFileName: false);

            // TODO: For proper regression testing of gifs, use a multi-frame reference output, or find a working reference decoder.
            // IImageDecoder referenceDecoder = TestEnvironment.Ge
            // ReferenceDecoder(testOutputFile);
            // using var encoded = Image.Load<TPixel>(testOutputFile, referenceDecoder);
            // ValidatorComparer.VerifySimilarity(image, encoded);
        }

        [Fact]
        public void NonMutatingEncodePreservesPaletteCount()
        {
            using (var inStream = new MemoryStream(TestFile.Create(TestImages.Gif.Leo).Bytes))
            using (var outStream = new MemoryStream())
            {
                inStream.Position = 0;

                var image = Image.Load<Rgba32>(inStream);
                GifMetadata metaData = image.Metadata.GetGifMetadata();
                GifFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetGifMetadata();
                GifColorTableMode colorMode = metaData.ColorTableMode;
                var encoder = new GifEncoder
                {
                    ColorTableMode = colorMode,
                    Quantizer = new OctreeQuantizer(new QuantizerOptions { MaxColors = frameMetadata.ColorTableLength })
                };

                image.Save(outStream, encoder);
                outStream.Position = 0;

                outStream.Position = 0;
                var clone = Image.Load<Rgba32>(outStream);

                GifMetadata cloneMetadata = clone.Metadata.GetGifMetadata();
                Assert.Equal(metaData.ColorTableMode, cloneMetadata.ColorTableMode);

                // Gifiddle and Cyotek GifInfo say this image has 64 colors.
                Assert.Equal(64, frameMetadata.ColorTableLength);

                for (int i = 0; i < image.Frames.Count; i++)
                {
                    GifFrameMetadata ifm = image.Frames[i].Metadata.GetGifMetadata();
                    GifFrameMetadata cifm = clone.Frames[i].Metadata.GetGifMetadata();

                    Assert.Equal(ifm.ColorTableLength, cifm.ColorTableLength);
                    Assert.Equal(ifm.FrameDelay, cifm.FrameDelay);
                }

                image.Dispose();
                clone.Dispose();
            }
        }
    }
}
