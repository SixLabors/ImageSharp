// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;
using static SixLabors.ImageSharp.Tests.TestImages.Webp;

namespace SixLabors.ImageSharp.Tests.Formats.Webp;

[Trait("Format", "Webp")]
public class WebpEncoderTests
{
    private static string TestImageLossyFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, Lossy.NoFilter06);

    [Theory]
    [WithFile(Lossless.Animated, PixelTypes.Rgba32)]
    public void Encode_AnimatedLossless<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossless,
            Quality = 100
        };

        // Always save as we need to compare the encoded output.
        provider.Utility.SaveTestOutputFile(image, "webp", encoder);

        // Compare encoded result
        image.VerifyEncoder(provider, "webp", string.Empty, encoder);
    }

    [Theory]
    [WithFile(Lossy.Animated, PixelTypes.Rgba32)]
    [WithFile(Lossy.AnimatedLandscape, PixelTypes.Rgba32)]
    public void Encode_AnimatedLossy<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossy,
            Quality = 100
        };

        // Always save as we need to compare the encoded output.
        provider.Utility.SaveTestOutputFile(image, "webp", encoder);

        // Compare encoded result
        // The reference decoder seems to produce differences up to 0.1% but the input/output have been
        // checked to be correct.
        string path = provider.Utility.GetTestOutputFileName("webp", null, true);
        using Image<Rgba32> encoded = Image.Load<Rgba32>(path);
        encoded.CompareToReferenceOutput(ImageComparer.Tolerant(0.01f), provider, null, "webp");
    }

    [Theory]
    [WithFile(TestImages.Gif.Leo, PixelTypes.Rgba32)]
    public void Encode_AnimatedFormatTransform_FromGif<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (TestEnvironment.RunsOnCI && !TestEnvironment.IsWindows)
        {
            return;
        }

        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance);
        using MemoryStream memStream = new();

        image.Save(memStream, new WebpEncoder());
        memStream.Position = 0;

        using Image<TPixel> output = Image.Load<TPixel>(memStream);

        ImageComparer.Exact.VerifySimilarity(output, image);

        GifMetadata gif = image.Metadata.GetGifMetadata();
        WebpMetadata webp = output.Metadata.GetWebpMetadata();

        Assert.Equal(gif.RepeatCount, webp.RepeatCount);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            GifFrameMetadata gifF = image.Frames[i].Metadata.GetGifMetadata();
            WebpFrameMetadata webpF = output.Frames[i].Metadata.GetWebpMetadata();

            Assert.Equal(gifF.FrameDelay, (int)(webpF.FrameDelay / 10));

            switch (gifF.DisposalMode)
            {
                case FrameDisposalMode.RestoreToBackground:
                    Assert.Equal(FrameDisposalMode.RestoreToBackground, webpF.DisposalMode);
                    break;
                case FrameDisposalMode.RestoreToPrevious:
                case FrameDisposalMode.Unspecified:
                case FrameDisposalMode.DoNotDispose:
                default:
                    Assert.Equal(FrameDisposalMode.DoNotDispose, webpF.DisposalMode);
                    break;
            }
        }
    }

    [Theory]
    // [WithFile(AlphaBlend, PixelTypes.Rgba32)]
    // [WithFile(AlphaBlend2, PixelTypes.Rgba32)]
    [WithFile(AlphaBlend3, PixelTypes.Rgba32)]
    // [WithFile(AlphaBlend4, PixelTypes.Rgba32)]
    public void Encode_AlphaBlended<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossless
        };

        QuantizerOptions options = new()
        {
            TransparencyThreshold = 128 / 255F
        };

        // First save as gif to gif using different quantizers with default options.
        // Alpha thresholding is 64/255F.
        GifEncoder gifEncoder = new()
        {
            Quantizer = new OctreeQuantizer(options)
        };
        provider.Utility.SaveTestOutputFile(image, "gif", gifEncoder, "octree");

        gifEncoder = new GifEncoder
        {
            Quantizer = new WuQuantizer(options)
        };
        provider.Utility.SaveTestOutputFile(image, "gif", gifEncoder, "wu");

        // Now clone and quantize the image using the same quantizers  without alpha thresholding and save as webp.
        options = new QuantizerOptions
        {
            TransparencyThreshold = 0
        };

        using Image<TPixel> cloned1 = image.Clone();
        cloned1.Mutate(c => c.Quantize(new OctreeQuantizer(options)));
        provider.Utility.SaveTestOutputFile(cloned1, "webp", encoder, "octree");

        using Image<TPixel> cloned2 = image.Clone();
        cloned2.Mutate(c => c.Quantize(new WuQuantizer(options)));
        provider.Utility.SaveTestOutputFile(cloned2, "webp", encoder, "wu");

        // Now blend the images with a blue background and save as webp.
        using Image<Rgba32> background1 = new(image.Width, image.Height, Color.White.ToPixel<Rgba32>());
        background1.Mutate(c => c.DrawImage(cloned1, 1));
        provider.Utility.SaveTestOutputFile(background1, "webp", encoder, "octree-blended");

        using Image<Rgba32> background2 = new(image.Width, image.Height, Color.White.ToPixel<Rgba32>());
        background2.Mutate(c => c.DrawImage(cloned2, 1));
        provider.Utility.SaveTestOutputFile(background2, "webp", encoder, "wu-blended");
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
        image.Save(memStream, new WebpEncoder());
        memStream.Position = 0;

        provider.Utility.SaveTestOutputFile(image, "gif", new GifEncoder());
        provider.Utility.SaveTestOutputFile(image, "png", new PngEncoder());
        provider.Utility.SaveTestOutputFile(image, "webp", new WebpEncoder());

        using Image<TPixel> output = Image.Load<TPixel>(memStream);
        ImageComparer.Exact.VerifySimilarity(output, image);
        PngMetadata png = image.Metadata.GetPngMetadata();
        WebpMetadata webp = output.Metadata.GetWebpMetadata();

        Assert.Equal(png.RepeatCount, webp.RepeatCount);

        for (int i = 0; i < image.Frames.Count; i++)
        {
            PngFrameMetadata pngF = image.Frames[i].Metadata.GetPngMetadata();
            WebpFrameMetadata webpF = output.Frames[i].Metadata.GetWebpMetadata();

            Assert.Equal((uint)(pngF.FrameDelay.ToDouble() * 1000), webpF.FrameDelay);

            switch (pngF.BlendMode)
            {
                case FrameBlendMode.Source:
                    Assert.Equal(FrameBlendMode.Source, webpF.BlendMode);
                    break;
                case FrameBlendMode.Over:
                default:
                    Assert.Equal(FrameBlendMode.Over, webpF.BlendMode);
                    break;
            }

            switch (pngF.DisposalMode)
            {
                case FrameDisposalMode.RestoreToBackground:
                    Assert.Equal(FrameDisposalMode.RestoreToBackground, webpF.DisposalMode);
                    break;
                case FrameDisposalMode.DoNotDispose:
                default:
                    Assert.Equal(FrameDisposalMode.DoNotDispose, webpF.DisposalMode);
                    break;
            }
        }
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Baseline.Jpeg410, PixelTypes.Rgba32, WebpFileFormatType.Lossy)] // Input is lossy jpeg.
    [WithFile(Flag, PixelTypes.Rgba32, WebpFileFormatType.Lossless)] // Input is lossless png.
    [WithFile(Lossless.NoTransform1, PixelTypes.Rgba32, WebpFileFormatType.Lossless)]
    [WithFile(Lossy.BikeWithExif, PixelTypes.Rgba32, WebpFileFormatType.Lossy)]
    public void Encode_PreserveEncodingType<TPixel>(TestImageProvider<TPixel> provider, WebpFileFormatType expectedFormat)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder options = new();
        using Image<TPixel> input = provider.GetImage();
        using MemoryStream memoryStream = new();
        input.Save(memoryStream, options);

        memoryStream.Position = 0;
        using Image<Rgba32> output = Image.Load<Rgba32>(memoryStream);

        ImageMetadata meta = output.Metadata;
        WebpMetadata webpMetaData = meta.GetWebpMetadata();
        Assert.Equal(expectedFormat, webpMetaData.FileFormat);
    }

    [Theory]
    [WithFile(Flag, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.PalettedTwoColor, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Paletted256Colors, PixelTypes.Rgba32)]
    public void Encode_Lossless_WithPalette_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossless,
            Quality = 100,
            Method = WebpEncodingMethod.BestQuality
        };

        using Image<TPixel> image = provider.GetImage();
        image.VerifyEncoder(provider, "webp", string.Empty, encoder);
    }

    [Theory]
    [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, 100)]
    [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, 80)]
    [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, 20)]
    public void Encode_Lossless_WithDifferentQuality_Works<TPixel>(TestImageProvider<TPixel> provider, int quality)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossless,
            Quality = quality
        };

        using Image<TPixel> image = provider.GetImage();
        string testOutputDetails = $"lossless_q{quality}";
        image.VerifyEncoder(provider, "webp", testOutputDetails, encoder);
    }

    [Theory]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6, 100)]
    public void Encode_Lossless_WithDifferentMethodAndQuality_Works<TPixel>(TestImageProvider<TPixel> provider, WebpEncodingMethod method, int quality)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossless,
            Method = method,
            Quality = quality
        };

        using Image<TPixel> image = provider.GetImage();
        string testOutputDetails = $"lossless_m{method}_q{quality}";
        image.VerifyEncoder(provider, "webp", testOutputDetails, encoder);
    }

    [Theory]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 15114)]
    public void Encode_Lossless_WithBestQuality_HasExpectedSize<TPixel>(TestImageProvider<TPixel> provider, int expectedBytes)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossless,
            Method = WebpEncodingMethod.BestQuality
        };

        using Image<TPixel> image = provider.GetImage();
        using MemoryStream memoryStream = new();
        image.Save(memoryStream, encoder);

        Assert.Equal(memoryStream.Length, expectedBytes);
    }

    [Theory]
    [WithFile(RgbTestPattern100x100, PixelTypes.Rgba32, 85)]
    [WithFile(RgbTestPattern100x100, PixelTypes.Rgba32, 60)]
    [WithFile(RgbTestPattern80x80, PixelTypes.Rgba32, 40)]
    [WithFile(RgbTestPattern80x80, PixelTypes.Rgba32, 20)]
    [WithFile(RgbTestPattern80x80, PixelTypes.Rgba32, 10)]
    [WithFile(RgbTestPattern63x63, PixelTypes.Rgba32, 40)]
    public void Encode_Lossless_WithNearLosslessFlag_Works<TPixel>(TestImageProvider<TPixel> provider, int nearLosslessQuality)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossless,
            NearLossless = true,
            NearLosslessQuality = nearLosslessQuality
        };

        using Image<TPixel> image = provider.GetImage();
        string testOutputDetails = $"nearlossless_q{nearLosslessQuality}";
        image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(nearLosslessQuality));
    }

    [Theory]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6)]
    [WithFile(Lossy.Alpha1, PixelTypes.Rgba32, 4)]
    public void Encode_Lossless_WithPreserveTransparentColor_Works<TPixel>(TestImageProvider<TPixel> provider, WebpEncodingMethod method)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossless,
            Method = method,
            TransparentColorMode = TransparentColorMode.Preserve
        };

        using Image<TPixel> image = provider.GetImage();
        string testOutputDetails = $"lossless_m{method}";
        image.VerifyEncoder(provider, "webp", testOutputDetails, encoder);
    }

    [Fact]
    public void Encode_Lossless_OneByOnePixel_Works()
    {
        // Just make sure, encoding 1 pixel by 1 pixel does not throw an exception.
        using Image<Rgba32> image = new(1, 1);
        WebpEncoder encoder = new() { FileFormat = WebpFileFormatType.Lossless };
        using (MemoryStream memStream = new())
        {
            image.SaveAsWebp(memStream, encoder);
        }
    }

    [Theory]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 20)]
    public void Encode_Lossy_WithDifferentQuality_Works<TPixel>(TestImageProvider<TPixel> provider, int quality)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossy,
            Quality = quality
        };

        using Image<TPixel> image = provider.GetImage();
        string testOutputDetails = $"lossy_q{quality}";
        image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(quality));
    }

    [Theory]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 80)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 50)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 30)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 10)]
    public void Encode_Lossy_WithDifferentFilterStrength_Works<TPixel>(TestImageProvider<TPixel> provider, int filterStrength)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossy,
            FilterStrength = filterStrength
        };

        using Image<TPixel> image = provider.GetImage();
        string testOutputDetails = $"lossy_f{filterStrength}";
        image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(75));
    }

    [Theory]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 80)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 50)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 30)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 10)]
    public void Encode_Lossy_WithDifferentSpatialNoiseShapingStrength_Works<TPixel>(TestImageProvider<TPixel> provider, int snsStrength)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossy,
            SpatialNoiseShaping = snsStrength
        };

        using Image<TPixel> image = provider.GetImage();
        string testOutputDetails = $"lossy_sns{snsStrength}";
        image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(75));
    }

    [Theory]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6, 75)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 0, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 1, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 2, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 3, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 4, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 5, 100)]
    [WithFile(Lossy.NoFilter06, PixelTypes.Rgba32, 6, 100)]
    public void Encode_Lossy_WithDifferentMethodsAndQuality_Works<TPixel>(TestImageProvider<TPixel> provider, WebpEncodingMethod method, int quality)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossy,
            Method = method,
            Quality = quality
        };

        using Image<TPixel> image = provider.GetImage();
        string testOutputDetails = $"lossy_m{method}_q{quality}";
        image.VerifyEncoder(provider, "webp", testOutputDetails, encoder, customComparer: GetComparer(quality));
    }

    [Theory]
    [WithFile(TestImages.Png.Transparency, PixelTypes.Rgba32)]
    public void Encode_Lossy_WithAlpha_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Floating point differences result in minor pixel differences affecting compression.
        // Output have been manually verified.
        int expectedFileSize = TestEnvironment.OSArchitecture == Architecture.Arm64 ? 64060 : 64020;

        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossy,
            UseAlphaCompression = false
        };

        using Image<TPixel> image = provider.GetImage();
        string encodedFile = image.VerifyEncoder(
            provider,
            "webp",
            "with_alpha",
            encoder,
            ImageComparer.Tolerant(0.04f),
            referenceDecoder: MagickReferenceDecoder.WebP);

        int encodedBytes = File.ReadAllBytes(encodedFile).Length;
        Assert.True(encodedBytes <= expectedFileSize, $"encoded bytes are {encodedBytes} and should be smaller then expected file size of {expectedFileSize}");
    }

    [Theory]
    [WithFile(TestImages.Png.Transparency, PixelTypes.Rgba32)]
    public void Encode_Lossy_WithAlphaUsingCompression_Works<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Floating point differences result in minor pixel differences affecting compression.
        // Output have been manually verified.
        int expectedFileSize = TestEnvironment.OSArchitecture == Architecture.Arm64 ? 16240 : 16200;

        WebpEncoder encoder = new()
        {
            FileFormat = WebpFileFormatType.Lossy,
            UseAlphaCompression = true
        };

        using Image<TPixel> image = provider.GetImage();
        string encodedFile = image.VerifyEncoder(
            provider,
            "webp",
            "with_alpha_compressed",
            encoder,
            ImageComparer.Tolerant(0.04f),
            referenceDecoder: MagickReferenceDecoder.WebP);

        int encodedBytes = File.ReadAllBytes(encodedFile).Length;
        Assert.True(encodedBytes <= expectedFileSize, $"encoded bytes are {encodedBytes} and should be smaller then expected file size of {expectedFileSize}");
    }

    [Theory]
    [WithFile(TestPatternOpaque, PixelTypes.Rgba32)]
    [WithFile(TestPatternOpaqueSmall, PixelTypes.Rgba32)]
    public void Encode_Lossless_WorksWithTestPattern<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        WebpEncoder encoder = new() { FileFormat = WebpFileFormatType.Lossless };
        image.VerifyEncoder(provider, "webp", string.Empty, encoder);
    }

    [Theory]
    [WithFile(TestPatternOpaque, PixelTypes.Rgba32)]
    [WithFile(TestPatternOpaqueSmall, PixelTypes.Rgba32)]
    public void Encode_Lossy_WorksWithTestPattern<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        WebpEncoder encoder = new() { FileFormat = WebpFileFormatType.Lossy };
        image.VerifyEncoder(provider, "webp", string.Empty, encoder, ImageComparer.Tolerant(0.04f));
    }

    // https://github.com/SixLabors/ImageSharp/issues/2763
    [Theory]
    [WithFile(Lossy.Issue2763, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Issue2763<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            Quality = 84,
            FileFormat = WebpFileFormatType.Lossless
        };

        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.VerifyEncoder(provider, "webp", string.Empty, encoder);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2801
    [Theory]
    [WithFile(Lossy.Issue2801, PixelTypes.Rgba32)]
    public void WebpDecoder_CanDecode_Issue2801<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        WebpEncoder encoder = new()
        {
            Quality = 100
        };

        using Image<TPixel> image = provider.GetImage();
        image.VerifyEncoder(provider, "webp", string.Empty, encoder, ImageComparer.TolerantPercentage(0.0994F));
    }

    public static void RunEncodeLossy_WithPeakImage()
    {
        TestImageProvider<Rgba32> provider = TestImageProvider<Rgba32>.File(TestImageLossyFullPath);
        using Image<Rgba32> image = provider.GetImage();

        WebpEncoder encoder = new() { FileFormat = WebpFileFormatType.Lossy };
        image.VerifyEncoder(provider, "webp", string.Empty, encoder, ImageComparer.Tolerant(0.04f));
    }

    [Fact]
    public void RunEncodeLossy_WithPeakImage_WithHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunEncodeLossy_WithPeakImage, HwIntrinsics.AllowAll);

    [Fact]
    public void RunEncodeLossy_WithPeakImage_WithoutHardwareIntrinsics_Works() => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunEncodeLossy_WithPeakImage, HwIntrinsics.DisableHWIntrinsic);

    [Theory]
    [WithFile(TestPatternOpaque, PixelTypes.Rgba32)]
    public void CanSave_NonSeekableStream<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        WebpEncoder encoder = new();

        using MemoryStream seekable = new();
        image.Save(seekable, encoder);

        using MemoryStream memoryStream = new();
        using NonSeekableStream nonSeekable = new(memoryStream);

        image.Save(nonSeekable, encoder);

        Assert.True(seekable.ToArray().SequenceEqual(memoryStream.ToArray()));
    }

    [Theory]
    [WithFile(TestPatternOpaque, PixelTypes.Rgba32)]
    public async Task CanSave_NonSeekableStream_Async<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        WebpEncoder encoder = new();

        await using MemoryStream seekable = new();
        image.Save(seekable, encoder);

        await using MemoryStream memoryStream = new();
        await using NonSeekableStream nonSeekable = new(memoryStream);

        await image.SaveAsync(nonSeekable, encoder);

        Assert.True(seekable.ToArray().SequenceEqual(memoryStream.ToArray()));
    }

    private static ImageComparer GetComparer(int quality)
    {
        float tolerance = 0.01f; // ~1.0%

        if (quality < 30)
        {
            tolerance = 0.02f; // ~2.0%
        }

        return ImageComparer.Tolerant(tolerance);
    }
}
