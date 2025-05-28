// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics.X86;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
[ValidateDisposedMemoryAllocations]
public partial class PngDecoderTests
{
    private const PixelTypes TestPixelTypes = PixelTypes.Rgba32 | PixelTypes.RgbaVector | PixelTypes.Argb32;

    public static readonly string[] CommonTestImages =
    {
        TestImages.Png.Splash,
        TestImages.Png.FilterVar,

        TestImages.Png.VimImage1,
        TestImages.Png.VimImage2,
        TestImages.Png.VersioningImage1,
        TestImages.Png.VersioningImage2,

        TestImages.Png.SnakeGame,

        TestImages.Png.Rgb24BppTrans,

        TestImages.Png.Bad.ChunkLength1,
        TestImages.Png.Bad.ChunkLength2,
    };

    public static readonly string[] TestImagesIssue1014 =
    {
        TestImages.Png.Issue1014_1, TestImages.Png.Issue1014_2,
        TestImages.Png.Issue1014_3, TestImages.Png.Issue1014_4,
        TestImages.Png.Issue1014_5, TestImages.Png.Issue1014_6
    };

    public static readonly string[] TestImagesIssue1177 =
    {
        TestImages.Png.Issue1177_1,
        TestImages.Png.Issue1177_2
    };

    public static readonly string[] CorruptedTestImages =
    {
        TestImages.Png.Bad.CorruptedChunk,
        TestImages.Png.Bad.ZlibOverflow,
        TestImages.Png.Bad.ZlibOverflow2,
        TestImages.Png.Bad.ZlibZtxtBadHeader,
    };

    public static readonly TheoryData<string, Type> PixelFormatRange = new()
    {
        { TestImages.Png.Gray4Bpp, typeof(Image<L8>) },
        { TestImages.Png.L16Bit, typeof(Image<L16>) },
        { TestImages.Png.Gray1BitTrans, typeof(Image<La16>) },
        { TestImages.Png.Gray2BitTrans, typeof(Image<La16>) },
        { TestImages.Png.Gray4BitTrans, typeof(Image<La16>) },
        { TestImages.Png.GrayA8Bit, typeof(Image<La16>) },
        { TestImages.Png.GrayAlpha16Bit, typeof(Image<La32>) },
        { TestImages.Png.Palette8Bpp, typeof(Image<Rgba32>) },
        { TestImages.Png.PalettedTwoColor, typeof(Image<Rgba32>) },
        { TestImages.Png.Rainbow, typeof(Image<Rgb24>) },
        { TestImages.Png.Rgb24BppTrans, typeof(Image<Rgba32>) },
        { TestImages.Png.Kaboom, typeof(Image<Rgba32>) },
        { TestImages.Png.Rgb48Bpp, typeof(Image<Rgb48>) },
        { TestImages.Png.Rgb48BppTrans, typeof(Image<Rgba64>) },
        { TestImages.Png.Rgba64Bpp, typeof(Image<Rgba64>) },
    };

    public static readonly string[] MultiFrameTestFiles =
    {
        TestImages.Png.APng,
        TestImages.Png.SplitIDatZeroLength,
        TestImages.Png.DisposeNone,
        TestImages.Png.DisposeBackground,
        TestImages.Png.DisposeBackgroundRegion,
        TestImages.Png.DisposePreviousFirst,
        TestImages.Png.DisposeBackgroundBeforeRegion,
        TestImages.Png.BlendOverMultiple,
        TestImages.Png.FrameOffset,
        TestImages.Png.DefaultNotAnimated
    };

    [Theory]
    [MemberData(nameof(PixelFormatRange))]
    public void Decode_NonGeneric_CreatesCorrectImageType(string path, Type type)
    {
        string file = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, path);
        using Image image = Image.Load(file);
        Assert.IsType(type, image);
    }

    [Theory]
    [MemberData(nameof(PixelFormatRange))]
    public async Task DecodeAsync_NonGeneric_CreatesCorrectImageType(string path, Type type)
    {
        string file = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, path);
        using Image image = await Image.LoadAsync(file);
        Assert.IsType(type, image);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32)]
    public void Decode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFileCollection(nameof(MultiFrameTestFiles), PixelTypes.Rgba32)]
    public void Decode_VerifyAllFrames<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);

        // Some images have many frames, only compare a selection of them.
        static bool Predicate(int i, int _) => i <= 8 || i % 8 == 0;
        image.DebugSaveMultiFrame(provider, predicate: Predicate);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact, predicate: Predicate);
    }

    [Theory]
    [WithFile(TestImages.Png.Splash, PixelTypes.Rgba32)]
    public void PngDecoder_Decode_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new()
        {
            TargetSize = new() { Width = 150, Height = 150 }
        };

        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance, options);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);

        // Floating point differences in FMA used in the ResizeKernel result in minor pixel differences.
        // Output have been manually verified.
        // For more details see discussion: https://github.com/SixLabors/ImageSharp/pull/1513#issuecomment-763643594
        image.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(Fma.IsSupported ? 0.0003F : 0.0005F),
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Png.Splash, PixelTypes.Rgba32)]
    public void PngDecoder_Decode_Resize_ScalarResizeKernel(TestImageProvider<Rgba32> provider)
    {
        HwIntrinsics intrinsicsFilter = HwIntrinsics.DisableHWIntrinsic;

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            intrinsicsFilter,
            provider,
            string.Empty);

        static void RunTest(string arg1, string notUsed)
        {
            TestImageProvider<Rgba32> provider =
                FeatureTestRunner.DeserializeForXunit<TestImageProvider<Rgba32>>(arg1);

            DecoderOptions options = new()
            {
                TargetSize = new() { Width = 150, Height = 150 }
            };

            using Image<Rgba32> image = provider.GetImage(PngDecoder.Instance, options);

            FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

            image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);

            image.CompareToReferenceOutput(
                ImageComparer.TolerantPercentage(0.0005F),
                provider,
                testOutputDetails: details,
                appendPixelTypeToFileName: false);
        }
    }

    [Theory]
    [WithFile(TestImages.Png.AverageFilter3BytesPerPixel, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.AverageFilter4BytesPerPixel, PixelTypes.Rgba32)]
    public void Decode_WithAverageFilter<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.SubFilter3BytesPerPixel, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.SubFilter4BytesPerPixel, PixelTypes.Rgba32)]
    public void Decode_WithSubFilter<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.UpFilter, PixelTypes.Rgba32)]
    public void Decode_WithUpFilter<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.PaethFilter3BytesPerPixel, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.PaethFilter4BytesPerPixel, PixelTypes.Rgba32)]
    public void Decode_WithPaethFilter<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.GrayA8Bit, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Gray1BitTrans, PixelTypes.Rgba32)]
    public void Decode_GrayWithAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.Interlaced, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Banner7Adam7InterlaceMode, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Banner8Index, PixelTypes.Rgba32)]
    public void Decode_Interlaced<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.Indexed, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Banner8Index, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.PalettedTwoColor, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.PalettedFourColor, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.PalettedSixteenColor, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Paletted256Colors, PixelTypes.Rgba32)]
    public void Decode_Indexed<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.Rgb48Bpp, PixelTypes.Rgb48)]
    [WithFile(TestImages.Png.Rgb48BppInterlaced, PixelTypes.Rgb48)]
    public void Decode_48Bpp<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.Rgba64Bpp, PixelTypes.Rgba64)]
    [WithFile(TestImages.Png.Rgb48BppTrans, PixelTypes.Rgba64)]
    public void Decode_64Bpp<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.GrayAlpha1BitInterlaced, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.GrayAlpha2BitInterlaced, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Gray4BitInterlaced, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.GrayA8BitInterlaced, PixelTypes.Rgba32)]
    public void Decoder_L8bitInterlaced<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.L16Bit, PixelTypes.Rgb48)]
    public void Decode_L16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.GrayAlpha16Bit, PixelTypes.Rgba64)]
    [WithFile(TestImages.Png.GrayTrns16BitInterlaced, PixelTypes.Rgba64)]
    public void Decode_GrayAlpha16Bit<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.GrayA8BitInterlaced, TestPixelTypes)]
    public void Decoder_CanDecode_Grey8bitInterlaced_WithAlpha<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFileCollection(nameof(CorruptedTestImages), PixelTypes.Rgba32)]
    public void Decoder_CanDecode_CorruptedImages<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Png.Splash, TestPixelTypes)]
    public void Decoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToOriginal(provider, ImageComparer.Exact);
    }

    [Theory]
    [InlineData(TestImages.Png.Bpp1, 1)]
    [InlineData(TestImages.Png.Gray4Bpp, 4)]
    [InlineData(TestImages.Png.Palette8Bpp, 8)]
    [InlineData(TestImages.Png.Pd, 24)]
    [InlineData(TestImages.Png.Blur, 32)]
    [InlineData(TestImages.Png.Rgb48Bpp, 48)]
    [InlineData(TestImages.Png.Rgb48BppInterlaced, 48)]
    public void Identify(string imagePath, int expectedPixelSize)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);

        Assert.NotNull(imageInfo);
        Assert.Equal(expectedPixelSize, imageInfo.PixelType.BitsPerPixel);
    }

    [Theory]
    [WithFile(TestImages.Png.Bad.MissingDataChunk, PixelTypes.Rgba32)]
    public void Decode_MissingDataChunk_ThrowsException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);
            });
        Assert.NotNull(ex);
        Assert.Contains("PNG Image does not contain a data chunk", ex.Message);
    }

    [Theory]
    [WithFile(TestImages.Png.Bad.MissingPaletteChunk1, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Bad.MissingPaletteChunk2, PixelTypes.Rgba32)]
    public void Decode_MissingPaletteChunk_ThrowsException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);
            });
        Assert.NotNull(ex);
        Assert.Contains("PNG Image does not contain a palette chunk", ex.Message);
    }

    [Theory]
    [WithFile(TestImages.Png.Bad.InvalidGammaChunk, PixelTypes.Rgba32)]
    public void Decode_InvalidGammaChunk_Ignored<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);
            });
        Assert.Null(ex);
    }

    [Theory]
    [WithFile(TestImages.Png.Bad.BitDepthZero, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Bad.BitDepthThree, PixelTypes.Rgba32)]
    public void Decode_InvalidBitDepth_ThrowsException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);
            });
        Assert.NotNull(ex);
        Assert.Contains("Invalid or unsupported bit depth", ex.Message);
    }

    [Theory]
    [WithFile(TestImages.Png.Bad.ColorTypeOne, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Bad.ColorTypeNine, PixelTypes.Rgba32)]
    public void Decode_InvalidColorType_ThrowsException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);
            });
        Assert.NotNull(ex);
        Assert.Contains("Invalid or unsupported color type", ex.Message);
    }

    [Theory]
    [WithFile(TestImages.Png.Bad.WrongCrcDataChunk, PixelTypes.Rgba32)]
    public void Decode_InvalidDataChunkCrc_ThrowsException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
            });
        Assert.NotNull(ex);
        Assert.Contains("CRC Error. PNG IDAT chunk is corrupt!", ex.Message);
    }

    // https://github.com/SixLabors/ImageSharp/pull/2589
    [Theory]
    [WithFile(TestImages.Png.Bad.WrongCrcDataChunk, PixelTypes.Rgba32, true)]
    [WithFile(TestImages.Png.Bad.Issue2589, PixelTypes.Rgba32, false)]
    public void Decode_InvalidDataChunkCrc_IgnoreCrcErrors<TPixel>(TestImageProvider<TPixel> provider, bool compare)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance, new PngDecoderOptions() { PngCrcChunkHandling = PngCrcChunkHandling.IgnoreData });

        image.DebugSave(provider);
        if (compare)
        {
            // Magick cannot actually decode this image to compare.
            image.CompareToOriginal(provider, new MagickReferenceDecoder(false));
        }
    }

    // https://github.com/SixLabors/ImageSharp/issues/1014
    [Theory]
    [WithFileCollection(nameof(TestImagesIssue1014), PixelTypes.Rgba32)]
    public void Issue1014_DataSplitOverMultipleIDatChunks<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            });
        Assert.Null(ex);
    }

    // https://github.com/SixLabors/ImageSharp/issues/1177
    [Theory]
    [WithFileCollection(nameof(TestImagesIssue1177), PixelTypes.Rgba32)]
    public void Issue1177_CRC_Omitted<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            });
        Assert.Null(ex);
    }

    // https://github.com/SixLabors/ImageSharp/issues/1127
    [Theory]
    [WithFile(TestImages.Png.Issue1127, PixelTypes.Rgba32)]
    public void Issue1127<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            });
        Assert.Null(ex);
    }

    // https://github.com/SixLabors/ImageSharp/issues/1047
    [Theory]
    [WithFile(TestImages.Png.Bad.Issue1047_BadEndChunk, PixelTypes.Rgba32)]
    public void Issue1047<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);

                // We don't have another x-plat reference decoder that can be compared for this image.
                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider, ImageComparer.Exact, SystemDrawingReferenceDecoder.Instance);
                }
            });
        Assert.Null(ex);
    }

    // https://github.com/SixLabors/ImageSharp/issues/1765
    [Theory]
    [WithFile(TestImages.Png.Issue1765_Net6DeflateStreamRead, PixelTypes.Rgba32)]
    public void Issue1765<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);
                image.CompareToOriginal(provider, ImageComparer.Exact);
            });
        Assert.Null(ex);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2209
    [Theory]
    [WithFile(TestImages.Png.Issue2209IndexedWithTransparency, PixelTypes.Rgba32)]
    public void Issue2209_Decode_HasTransparencyIsTrue<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        PngMetadata metadata = image.Metadata.GetPngMetadata();
        Assert.NotNull(metadata.ColorTable);
        Assert.Contains(metadata.ColorTable.Value.ToArray(), x => x.ToRgba32().A < 255);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2209
    [Theory]
    [InlineData(TestImages.Png.Issue2209IndexedWithTransparency)]
    public void Issue2209_Identify_HasTransparencyIsTrue(string imagePath)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);
        ImageInfo imageInfo = Image.Identify(stream);
        PngMetadata metadata = imageInfo.Metadata.GetPngMetadata();
        Assert.NotNull(metadata.ColorTable);
        Assert.Contains(metadata.ColorTable.Value.ToArray(), x => x.ToRgba32().A < 255);
    }

    // https://github.com/SixLabors/ImageSharp/issues/410
    [Theory]
    [WithFile(TestImages.Png.Bad.Issue410_MalformedApplePng, PixelTypes.Rgba32)]
    public void Issue410_MalformedApplePng<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
                image.DebugSave(provider);

                // We don't have another x-plat reference decoder that can be compared for this image.
                if (TestEnvironment.IsWindows)
                {
                    image.CompareToOriginal(provider, ImageComparer.Exact, SystemDrawingReferenceDecoder.Instance);
                }
            });
        Assert.NotNull(ex);
        Assert.Contains("Proprietary Apple PNG detected!", ex.Message);
    }

    [Theory]
    [WithFile(TestImages.Png.Splash, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Bike, PixelTypes.Rgba32)]
    public void PngDecoder_DegenerateMemoryRequest_ShouldTranslateTo_ImageFormatException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InPixelsSqrt(10);
        InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() => provider.GetImage(PngDecoder.Instance));
        Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
    }

    [Theory]
    [WithFile(TestImages.Png.Splash, PixelTypes.Rgba32)]
    [WithFile(TestImages.Png.Bike, PixelTypes.Rgba32)]
    public void PngDecoder_CanDecode_WithLimitedAllocatorBufferCapacity(TestImageProvider<Rgba32> provider)
    {
        static void RunTest(string providerDump, string nonContiguousBuffersStr)
        {
            TestImageProvider<Rgba32> provider = BasicSerializer.Deserialize<TestImageProvider<Rgba32>>(providerDump);

            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);

            using Image<Rgba32> image = provider.GetImage(PngDecoder.Instance);
            image.DebugSave(provider, testOutputDetails: nonContiguousBuffersStr);
            image.CompareToOriginal(provider);
        }

        string providerDump = BasicSerializer.Serialize(provider);
        RemoteExecutor.Invoke(
                RunTest,
                providerDump,
                "Disco")
            .Dispose();
    }

    [Fact]
    public void Binary_PrematureEof()
    {
        PngDecoder decoder = PngDecoder.Instance;
        PngDecoderOptions options = new() { PngCrcChunkHandling = PngCrcChunkHandling.IgnoreData };
        using EofHitCounter eofHitCounter = EofHitCounter.RunDecoder(TestImages.Png.Bad.FlagOfGermany0000016446, decoder, options);

        // TODO: Try to reduce this to 1.
        Assert.True(eofHitCounter.EofHitCount <= 3);
        Assert.Equal(new Size(200, 120), eofHitCounter.Image.Size);
    }

    [Fact]
    public void Decode_Issue2666()
    {
        string path = Path.GetFullPath(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Png.Issue2666));
        using Image image = Image.Load(path);
    }

    [Theory]

    [InlineData(TestImages.Png.Bad.BadZTXT)]
    [InlineData(TestImages.Png.Bad.BadZTXT2)]
    public void Decode_BadZTXT(string file)
    {
        string path = Path.GetFullPath(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, file));
        using Image image = Image.Load(path);
    }

    [Theory]
    [InlineData(TestImages.Png.Bad.BadZTXT)]
    [InlineData(TestImages.Png.Bad.BadZTXT2)]
    public void Info_BadZTXT(string file)
    {
        string path = Path.GetFullPath(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, file));
        _ = Image.Identify(path);
    }

    [Theory]
    [InlineData(TestImages.Png.Bad.Issue2714BadPalette)]
    public void Decode_BadPalette(string file)
    {
        string path = Path.GetFullPath(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, file));
        using Image image = Image.Load(path);
    }

    [Theory]
    [WithFile(TestImages.Png.Issue2752, PixelTypes.Rgba32)]
    public void CanDecodeJustOneFrame<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new() { MaxFrames = 1 };
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance, options);
        Assert.Equal(1, image.Frames.Count);
    }

    [Theory]
    [WithFile(TestImages.Png.Issue2924, PixelTypes.Rgba32)]
    public void CanDecode_Issue2924<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(PngDecoder.Instance);
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }
}
