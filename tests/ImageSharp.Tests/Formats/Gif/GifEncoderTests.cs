// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Formats.Gif
{
    public class GifEncoderTests
    {
        private const PixelTypes TestPixelTypes = PixelTypes.Rgba32 | PixelTypes.RgbaVector | PixelTypes.Argb32;
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.001F);

        public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
        new TheoryData<string, int, int, PixelResolutionUnit>
        {
            { TestImages.Gif.Rings, (int)ImageMetaData.DefaultHorizontalResolution, (int)ImageMetaData.DefaultVerticalResolution , PixelResolutionUnit.PixelsPerInch},
            { TestImages.Gif.Ratio1x4, 1, 4 , PixelResolutionUnit.AspectRatio},
            { TestImages.Gif.Ratio4x1, 4, 1, PixelResolutionUnit.AspectRatio }
        };

        [Theory]
        [WithTestPatternImages(100, 100, TestPixelTypes)]
        public void EncodeGeneratedPatterns<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var encoder = new GifEncoder()
                {
                    // Use the palette quantizer without dithering to ensure results 
                    // are consistant
                    Quantizer = new PaletteQuantizer(false)
                };

                // Always save as we need to compare the encoded output.
                provider.Utility.SaveTestOutputFile(image, "gif", encoder);
            }

            // Compare encoded result
            string path = provider.Utility.GetTestOutputFileName("gif", null, true);
            using (var encoded = Image.Load(path))
            {
                encoded.CompareToReferenceOutput(ValidatorComparer, provider, null, "gif");
            }
        }

        [Theory]
        [MemberData(nameof(RatioFiles))]
        public void Encode_PreserveRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
        {
            var options = new GifEncoder()
            {
                IgnoreMetadata = false
            };

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

        [Fact]
        public void Encode_IgnoreMetadataIsFalse_CommentsAreWritten()
        {
            var options = new GifEncoder()
            {
                IgnoreMetadata = false
            };

            var testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> input = testFile.CreateImage())
            {
                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, options);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        Assert.Equal(1, output.MetaData.Properties.Count);
                        Assert.Equal("Comments", output.MetaData.Properties[0].Name);
                        Assert.Equal("ImageSharp", output.MetaData.Properties[0].Value);
                    }
                }
            }
        }

        [Fact]
        public void Encode_IgnoreMetadataIsTrue_CommentsAreNotWritten()
        {
            var options = new GifEncoder()
            {
                IgnoreMetadata = true
            };

            var testFile = TestFile.Create(TestImages.Gif.Rings);

            using (Image<Rgba32> input = testFile.CreateImage())
            {
                using (var memStream = new MemoryStream())
                {
                    input.SaveAsGif(memStream, options);

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        Assert.Equal(0, output.MetaData.Properties.Count);
                    }
                }
            }
        }

        [Fact]
        public void Encode_WhenCommentIsTooLong_CommentIsTrimmed()
        {
            using (var input = new Image<Rgba32>(1, 1))
            {
                string comments = new string('c', 256);
                input.MetaData.Properties.Add(new ImageProperty("Comments", comments));

                using (var memStream = new MemoryStream())
                {
                    input.Save(memStream, new GifEncoder());

                    memStream.Position = 0;
                    using (var output = Image.Load<Rgba32>(memStream))
                    {
                        Assert.Equal(1, output.MetaData.Properties.Count);
                        Assert.Equal("Comments", output.MetaData.Properties[0].Name);
                        Assert.Equal(255, output.MetaData.Properties[0].Value.Length);
                    }
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Cheers, PixelTypes.Rgba32)]
        public void EncodeGlobalPaletteReturnsSmallerFile<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var encoder = new GifEncoder
                {
                    ColorTableMode = GifColorTableMode.Global,
                    Quantizer = new OctreeQuantizer(false)
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

        [Fact]
        public void NonMutatingEncodePreservesPaletteCount()
        {
            using (var inStream = new MemoryStream(TestFile.Create(TestImages.Gif.Leo).Bytes))
            using (var outStream = new MemoryStream())
            {
                var info = (GifInfo)Image.Identify(inStream);
                GifColorTableMode colorMode = info.ColorTableMode;
                inStream.Position = 0;

                var image = Image.Load(inStream);
                var encoder = new GifEncoder()
                {
                    ColorTableMode = colorMode,
                    Quantizer = new OctreeQuantizer(image.Frames.RootFrame.MetaData.ColorTableLength)
                };

                image.Save(outStream, encoder);
                outStream.Position = 0;

                var cloneInfo = (GifInfo)Image.Identify(outStream);
                outStream.Position = 0;
                var clone = Image.Load(outStream);

                // Gifiddle and Cyotek GifInfo say this image has 64 colors.
                Assert.Equal(64, image.Frames.RootFrame.MetaData.ColorTableLength);
                Assert.Equal(info.ColorTableMode, cloneInfo.ColorTableMode);

                for (int i = 0; i < image.Frames.Count; i++)
                {
                    Assert.Equal(image.Frames[i].MetaData.ColorTableLength, clone.Frames[i].MetaData.ColorTableLength);
                    Assert.Equal(image.Frames[i].MetaData.FrameDelay, clone.Frames[i].MetaData.FrameDelay);
                }

                image.Dispose();
                clone.Dispose();
            }
        }
    }
}
