// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using System.Linq;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Gif;

[Trait("Format", "Gif")]
public class GifEncoderTests
{
    private const PixelTypes TestPixelTypes = PixelTypes.Rgba32 | PixelTypes.RgbaVector | PixelTypes.Argb32;
    private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.0015F);

    public static readonly TheoryData<string, int, int, PixelResolutionUnit> RatioFiles =
    new()
    {
        { TestImages.Gif.Rings, (int)ImageMetadata.DefaultHorizontalResolution, (int)ImageMetadata.DefaultVerticalResolution, PixelResolutionUnit.PixelsPerInch },
        { TestImages.Gif.Ratio1x4, 1, 4, PixelResolutionUnit.AspectRatio },
        { TestImages.Gif.Ratio4x1, 4, 1, PixelResolutionUnit.AspectRatio }
    };

    public GifEncoderTests()
    {
        // Free the pool on 32 bit:
        if (!TestEnvironment.Is64BitProcess)
        {
            Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
        }
    }

    [Fact]
    public void GifEncoderDefaultInstanceHasNullQuantizer() => Assert.Null(new GifEncoder().Quantizer);

    [Theory]
    [WithTestPatternImages(100, 100, TestPixelTypes, false)]
    [WithTestPatternImages(100, 100, TestPixelTypes, true)]
    public void EncodeGeneratedPatterns<TPixel>(TestImageProvider<TPixel> provider, bool limitAllocationBuffer)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (limitAllocationBuffer)
        {
            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);
        }

        using (Image<TPixel> image = provider.GetImage())
        {
            GifEncoder encoder = new()
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
        using Image<Rgba32> encoded = Image.Load<Rgba32>(path);
        encoded.CompareToReferenceOutput(ValidatorComparer, provider, null, "gif");
    }

    [Theory]
    [MemberData(nameof(RatioFiles))]
    public void Encode_PreserveRatio(string imagePath, int xResolution, int yResolution, PixelResolutionUnit resolutionUnit)
    {
        GifEncoder options = new();

        TestFile testFile = TestFile.Create(imagePath);
        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, options);

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        ImageMetadata meta = output.Metadata;
        Assert.Equal(xResolution, meta.HorizontalResolution);
        Assert.Equal(yResolution, meta.VerticalResolution);
        Assert.Equal(resolutionUnit, meta.ResolutionUnits);
    }

    [Fact]
    public void Encode_IgnoreMetadataIsFalse_CommentsAreWritten()
    {
        GifEncoder options = new();

        TestFile testFile = TestFile.Create(TestImages.Gif.Rings);

        using Image<Rgba32> input = testFile.CreateRgba32Image();
        using MemoryStream memStream = new();
        input.Save(memStream, options);

        memStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memStream);
        GifMetadata metadata = output.Metadata.GetGifMetadata();
        Assert.Equal(1, metadata.Comments.Count);
        Assert.Equal("ImageSharp", metadata.Comments[0]);
    }

    [Theory]
    [WithFile(TestImages.Gif.Cheers, PixelTypes.Rgba32)]
    public void EncodeGlobalPaletteReturnsSmallerFile<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        GifEncoder encoder = new()
        {
            ColorTableMode = GifColorTableMode.Global,
            Quantizer = new OctreeQuantizer(new QuantizerOptions { Dither = null })
        };

        // Always save as we need to compare the encoded output.
        provider.Utility.SaveTestOutputFile(image, "gif", encoder, "global");

        encoder = new()
        {
            ColorTableMode = GifColorTableMode.Local,
            Quantizer = new OctreeQuantizer(new QuantizerOptions { Dither = null }),
        };

        provider.Utility.SaveTestOutputFile(image, "gif", encoder, "local");

        FileInfo fileInfoGlobal = new(provider.Utility.GetTestOutputFileName("gif", "global"));
        FileInfo fileInfoLocal = new(provider.Utility.GetTestOutputFileName("gif", "local"));

        Assert.True(fileInfoGlobal.Length < fileInfoLocal.Length);
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

        GifEncoder encoder = new()
        {
            ColorTableMode = GifColorTableMode.Global,
            PixelSamplingStrategy = new DefaultPixelSamplingStrategy(maxPixels, scanRatio)
        };

        string testOutputFile = provider.Utility.SaveTestOutputFile(
            image,
            "gif",
            encoder,
            testOutputDetails: $"{maxPixels}_{scanRatio}",
            appendPixelTypeToFileName: false);

        // TODO: For proper regression testing of gifs, use a multi-frame reference output, or find a working reference decoder.
        // IImageDecoder referenceDecoder = TestEnvironment.GetReferenceDecoder(testOutputFile);
        // using var encoded = Image.Load<TPixel>(testOutputFile, referenceDecoder);
        // ValidatorComparer.VerifySimilarity(image, encoded);
    }

    [Fact]
    public void NonMutatingEncodePreservesPaletteCount()
    {
        using MemoryStream inStream = new(TestFile.Create(TestImages.Gif.Leo).Bytes);
        using MemoryStream outStream = new();
        inStream.Position = 0;

        Image<Rgba32> image = Image.Load<Rgba32>(inStream);
        GifMetadata metaData = image.Metadata.GetGifMetadata();
        GifFrameMetadata frameMetadata = image.Frames.RootFrame.Metadata.GetGifMetadata();
        GifColorTableMode colorMode = metaData.ColorTableMode;

        int maxColors;
        if (colorMode == GifColorTableMode.Global)
        {
            maxColors = metaData.GlobalColorTable.Value.Length;
        }
        else
        {
            maxColors = frameMetadata.LocalColorTable.Value.Length;
        }

        GifEncoder encoder = new()
        {
            ColorTableMode = colorMode,
            Quantizer = new OctreeQuantizer(new QuantizerOptions { MaxColors = maxColors })
        };

        image.Save(outStream, encoder);
        outStream.Position = 0;

        outStream.Position = 0;
        Image<Rgba32> clone = Image.Load<Rgba32>(outStream);

        GifMetadata cloneMetadata = clone.Metadata.GetGifMetadata();
        Assert.Equal(metaData.ColorTableMode, cloneMetadata.ColorTableMode);

        // Gifiddle and Cyotek GifInfo say this image has 64 colors.
        colorMode = cloneMetadata.ColorTableMode;
        if (colorMode == GifColorTableMode.Global)
        {
            maxColors = metaData.GlobalColorTable.Value.Length;
        }
        else
        {
            maxColors = frameMetadata.LocalColorTable.Value.Length;
        }

        Assert.Equal(64, maxColors);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            GifFrameMetadata iMeta = image.Frames[i].Metadata.GetGifMetadata();
            GifFrameMetadata cMeta = clone.Frames[i].Metadata.GetGifMetadata();

            if (iMeta.ColorTableMode == GifColorTableMode.Local)
            {
                Assert.Equal(iMeta.LocalColorTable.Value.Length, cMeta.LocalColorTable.Value.Length);
            }

            Assert.Equal(iMeta.FrameDelay, cMeta.FrameDelay);
        }

        image.Dispose();
        clone.Dispose();
    }

    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2288_A, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.Issues.Issue2288_B, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.Issues.Issue2288_C, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.Issues.Issue2288_D, PixelTypes.Rgba32)]
    public void OptionalExtensionsShouldBeHandledProperly<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        int count = 0;
        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            if (frame.Metadata.TryGetGifMetadata(out GifFrameMetadata _))
            {
                count++;
            }
        }

        provider.Utility.SaveTestOutputFile(image, extension: "gif");

        using FileStream fs = File.OpenRead(provider.Utility.GetTestOutputFileName("gif"));
        using Image<TPixel> image2 = Image.Load<TPixel>(fs);

        Assert.Equal(image.Frames.Count, image2.Frames.Count);

        count = 0;
        foreach (ImageFrame<TPixel> frame in image2.Frames)
        {
            if (frame.Metadata.TryGetGifMetadata(out GifFrameMetadata _))
            {
                count++;
            }
        }

        Assert.Equal(image2.Frames.Count, count);
    }

    [Theory]
    [WithFile(TestImages.Png.APng, PixelTypes.Rgba32)]
    public void Encode_AnimatedFormatTransform_FromPng<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (TestEnvironment.RunsOnCI && !TestEnvironment.IsWindows)
        {
            return;
        }

        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);

        using MemoryStream memStream = new();
        image.Save(memStream, new GifEncoder());
        memStream.Position = 0;

        using Image<TPixel> output = Image.Load<TPixel>(memStream);

        // TODO: Find a better way to compare.
        // The image has been visually checked but the quantization and frame trimming pattern used in the gif encoder
        // means we cannot use an exact comparison nor replicate using the quantizing processor.
        ImageComparer.TolerantPercentage(1.51f).VerifySimilarity(output, image);

        PngMetadata png = image.Metadata.GetPngMetadata();
        GifMetadata gif = output.Metadata.GetGifMetadata();

        Assert.Equal(png.RepeatCount, gif.RepeatCount);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            PngFrameMetadata pngF = image.Frames[i].Metadata.GetPngMetadata();
            GifFrameMetadata gifF = output.Frames[i].Metadata.GetGifMetadata();

            Assert.Equal((int)(pngF.FrameDelay.ToDouble() * 100), gifF.FrameDelay);

            switch (pngF.DisposalMethod)
            {
                case PngDisposalMethod.RestoreToBackground:
                    Assert.Equal(GifDisposalMethod.RestoreToBackground, gifF.DisposalMethod);
                    break;
                case PngDisposalMethod.DoNotDispose:
                default:
                    Assert.Equal(GifDisposalMethod.NotDispose, gifF.DisposalMethod);
                    break;
            }
        }
    }

    [Theory]
    [WithFile(TestImages.Webp.Lossless.Animated, PixelTypes.Rgba32)]
    public void Encode_AnimatedFormatTransform_FromWebp<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (TestEnvironment.RunsOnCI && !TestEnvironment.IsWindows)
        {
            return;
        }

        using Image<TPixel> image = provider.GetImage(WebpDecoder.Instance);

        using MemoryStream memStream = new();
        image.Save(memStream, new GifEncoder());
        memStream.Position = 0;

        using Image<TPixel> output = Image.Load<TPixel>(memStream);

        image.Save(provider.Utility.GetTestOutputFileName("gif"), new GifEncoder());

        // TODO: Find a better way to compare.
        // The image has been visually checked but the quantization and frame trimming pattern used in the gif encoder
        // means we cannot use an exact comparison nor replicate using the quantizing processor.
        ImageComparer.TolerantPercentage(0.776f).VerifySimilarity(output, image);

        WebpMetadata webp = image.Metadata.GetWebpMetadata();
        GifMetadata gif = output.Metadata.GetGifMetadata();

        Assert.Equal(webp.RepeatCount, gif.RepeatCount);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            WebpFrameMetadata webpF = image.Frames[i].Metadata.GetWebpMetadata();
            GifFrameMetadata gifF = output.Frames[i].Metadata.GetGifMetadata();

            Assert.Equal(webpF.FrameDelay, (uint)(gifF.FrameDelay * 10));

            switch (webpF.DisposalMethod)
            {
                case WebpDisposalMethod.RestoreToBackground:
                    Assert.Equal(GifDisposalMethod.RestoreToBackground, gifF.DisposalMethod);
                    break;
                case WebpDisposalMethod.DoNotDispose:
                default:
                    Assert.Equal(GifDisposalMethod.NotDispose, gifF.DisposalMethod);
                    break;
            }
        }
    }

    public static string[] Animated => TestImages.Gif.Animated;

    [Theory(Skip = "Enable for visual animated testing")]
    [WithFileCollection(nameof(Animated), PixelTypes.Rgba32)]
    public void Encode_Animated_VisualTest<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        provider.Utility.SaveTestOutputFile(image, "webp", new WebpEncoder() { FileFormat = WebpFileFormatType.Lossless }, "animated");
        provider.Utility.SaveTestOutputFile(image, "webp", new WebpEncoder() { FileFormat = WebpFileFormatType.Lossy }, "animated-lossy");
        provider.Utility.SaveTestOutputFile(image, "png", new PngEncoder(), "animated");
        provider.Utility.SaveTestOutputFile(image, "gif", new GifEncoder(), "animated");
    }

    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2866, PixelTypes.Rgba32)]
    public void GifEncoder_CanDecode_Issue2866<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        bool anyGlobal = ((IEnumerable<ImageFrame>)image.Frames).Any(x => x.Metadata.GetGifMetadata().ColorTableMode == GifColorTableMode.Global);

        // image.DebugSaveMultiFrame(provider);
        provider.Utility.SaveTestOutputFile(image, "gif", new GifEncoder(), "animated");
    }
}
