// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Gif;

[Trait("Format", "Gif")]
[ValidateDisposedMemoryAllocations]
public class GifDecoderTests
{
    private const PixelTypes TestPixelTypes = PixelTypes.Rgba32 | PixelTypes.RgbaVector | PixelTypes.Argb32;

    public static readonly string[] MultiFrameTestFiles =
    [
        TestImages.Gif.Giphy, TestImages.Gif.Kumin
    ];

    /// <summary>
    /// Preserve and Convert use the ICC-transformed palette for pixels and metadata while retaining profiles by policy.
    /// </summary>
    /// <typeparam name="TPixel">The output pixel type.</typeparam>
    /// <param name="provider">The GIF fixture provider.</param>
    /// <param name="skipMetadata">Whether optional metadata is skipped.</param>
    [Theory]
    [WithFile(TestImages.Gif.ExifToolIcc, PixelTypes.Rgba32 | PixelTypes.Rgba64 | PixelTypes.RgbaVector, false)]
    [WithFile(TestImages.Gif.ExifToolIcc, PixelTypes.Rgba32 | PixelTypes.Rgba64 | PixelTypes.RgbaVector, true)]
    public void Decode_ExifToolIcc_ConvertsPaletteAndAppliesRetention<TPixel>(TestImageProvider<TPixel> provider, bool skipMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
        => AssertExifToolIccPaletteAndRetention<TPixel>(TestFile.Create(provider.SourceFileOrDescription).Bytes, skipMetadata);

    // Use the same fixture bytes and assertions for ordinary and associated destination pixel types.
    private static void AssertExifToolIccPaletteAndRetention<TPixel>(byte[] source, bool skipMetadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions preserveOptions = new(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Preserve, SkipMetadata = skipMetadata };
        DecoderOptions convertOptions = new(Configuration.Default) { ColorProfileHandling = ColorProfileHandling.Convert, SkipMetadata = skipMetadata };

        ImageInfo preservedInfo = Image.Identify(preserveOptions, source);
        ImageInfo convertedInfo = Image.Identify(convertOptions, source);

        // GIF's six-byte header and seven-byte logical screen descriptor precede this fixture's 256-entry global RGB table.
        ReadOnlySpan<Rgb24> encodedPalette = MemoryMarshal.Cast<byte, Rgb24>(source.AsSpan(13, 256 * 3));

        using Image<TPixel> preserved = Image.Load<TPixel>(preserveOptions, source);
        using Image<TPixel> converted = Image.Load<TPixel>(convertOptions, source);

        ColorConversionOptions conversionOptions = new()
        {
            SourceIccProfile = preservedInfo.Metadata.IccProfile,
            TargetIccProfile = CompactSrgbV4Profile.Profile
        };

        ColorProfileConverter converter = new(conversionOptions);
        Rgb[] expectedPalette = new Rgb[encodedPalette.Length];

        // Convert the encoded lookup once to keep the expected palette independent of GIF pixel expansion.
        for (int i = 0; i < encodedPalette.Length; i++)
        {
            expectedPalette[i] = Rgb.FromScaledVector4(encodedPalette[i].ToScaledVector4());
        }

        converter.Convert<Rgb, Rgb>(expectedPalette, expectedPalette);

        ReadOnlySpan<Color> identifiedPalette = preservedInfo.Metadata.GetGifMetadata().GlobalColorTable.Value.Span;
        ReadOnlySpan<Color> convertedInfoPalette = convertedInfo.Metadata.GetGifMetadata().GlobalColorTable.Value.Span;
        ReadOnlySpan<Color> preservedPalette = preserved.Metadata.GetGifMetadata().GlobalColorTable.Value.Span;
        ReadOnlySpan<Color> convertedPalette = converted.Metadata.GetGifMetadata().GlobalColorTable.Value.Span;

        Assert.Equal(256, identifiedPalette.Length);
        Assert.Equal(256, convertedInfoPalette.Length);
        Assert.Equal(256, preservedPalette.Length);
        Assert.Equal(256, convertedPalette.Length);

        for (int i = 0; i < encodedPalette.Length; i++)
        {
            Color expectedIdentified = Color.FromScaledVector(expectedPalette[i].ToScaledVector4());
            Color expected = Color.FromPixel(TPixel.FromUnassociatedScaledVector4(expectedPalette[i].ToScaledVector4()));

            Assert.Equal(expectedIdentified, identifiedPalette[i]);
            Assert.Equal(expectedIdentified, convertedInfoPalette[i]);
            Assert.Equal(expected, preservedPalette[i]);
            Assert.Equal(expected, convertedPalette[i]);
        }

        Assert.NotEqual(Color.FromPixel(encodedPalette[100]), identifiedPalette[100]);
        Assert.Equal(identifiedPalette[0], preservedInfo.Metadata.GetGifMetadata().BackgroundColor);
        Assert.Equal(convertedInfoPalette[0], convertedInfo.Metadata.GetGifMetadata().BackgroundColor);
        Assert.Equal(preservedPalette[0], preserved.Metadata.GetGifMetadata().BackgroundColor);
        Assert.Equal(convertedPalette[0], converted.Metadata.GetGifMetadata().BackgroundColor);
        Assert.Equal(preservedInfo.Metadata.IccProfile.ToByteArray(), preserved.Metadata.IccProfile.ToByteArray());
        Assert.Null(convertedInfo.Metadata.IccProfile);
        Assert.Null(converted.Metadata.IccProfile);

        // Every encoded pixel uses palette index 35, so one expected color covers the whole 8x8 frame.
        TPixel expectedPixel = Color.FromScaledVector(expectedPalette[35].ToScaledVector4()).ToPixel<TPixel>();

        for (int y = 0; y < preserved.Height; y++)
        {
            for (int x = 0; x < preserved.Width; x++)
            {
                Assert.Equal(expectedPixel, preserved[x, y]);
            }
        }

        ImageComparer.Exact.VerifySimilarity(preserved, converted);
    }

    /// <summary>
    /// Verifies that associated destination pixels retain their alpha representation in converted GIF palettes.
    /// </summary>
    /// <param name="skipMetadata">Whether optional metadata is skipped.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Decode_ExifToolIcc_ConvertsAssociatedPaletteAndAppliesRetention(bool skipMetadata)
    {
        byte[] source = TestFile.Create(TestImages.Gif.ExifToolIcc).Bytes;
        AssertExifToolIccPaletteAndRetention<Rgba32P>(source, skipMetadata);
        AssertExifToolIccPaletteAndRetention<RgbaVectorP>(source, skipMetadata);
    }

    /// <summary>
    /// The four palette colors in the stored frame geometry fixtures.
    /// </summary>
    private static readonly Rgba32[] GeometryTestColorTable =
    [
        new(255, 0, 0), new(0, 255, 0), new(0, 0, 255), new(255, 255, 255)
    ];

    [Theory]
    [WithFileCollection(nameof(MultiFrameTestFiles), PixelTypes.Rgba32)]
    public void Decode_VerifyAllFrames<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSaveMultiFrame(provider);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Gif.AnimatedLoop, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.AnimatedLoopInterlaced, PixelTypes.Rgba32)]
    public void Decode_Animated<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSaveMultiFrame(provider);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Gif.AnimatedTransparentNoRestore, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.AnimatedTransparentRestorePrevious, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.AnimatedTransparentLoop, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.AnimatedTransparentFirstFrameRestorePrev, PixelTypes.Rgba32)]
    public void Decode_Animated_WithTransparency<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSaveMultiFrame(provider);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
    }

    [Theory]
    [WithFile(TestImages.Gif.StaticNontransparent, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.StaticTransparent, PixelTypes.Rgba32)]
    public void Decode_Static_No_Animation<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2450_A, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.Issues.Issue2450_B, PixelTypes.Rgba32)]
    public void Decode_Issue2450<TPixel>(TestImageProvider<TPixel> provider)
    where TPixel : unmanaged, IPixel<TPixel>
    {
        // Images have many frames, only compare a selection of them.
        static bool Predicate(int i, int _) => i % 8 == 0;

        using Image<TPixel> image = provider.GetImage();
        image.DebugSaveMultiFrame(provider, predicate: Predicate);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact, predicate: Predicate);
    }

    [Theory]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
    public void GifDecoder_Decode_Resize<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new(Configuration.Default)
        {
            TargetSize = new Size { Width = 150, Height = 150 },
            MaxFrames = 1
        };

        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, options);

        FormattableString details = $"{options.TargetSize.Value.Width}_{options.TargetSize.Value.Height}";

        image.DebugSave(provider, testOutputDetails: details, appendPixelTypeToFileName: false);

        // Floating point differences in FMA used in the ResizeKernel result in minor pixel differences.
        // Output have been manually verified.
        // For more details see discussion: https://github.com/SixLabors/ImageSharp/pull/1513#issuecomment-763643594
        image.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(Fma.IsSupported ? 0.0001F : 0.0002F),
            provider,
            testOutputDetails: details,
            appendPixelTypeToFileName: false);
    }

    [Fact]
    public unsafe void Decode_NonTerminatedFinalFrame()
    {
        TestFile testFile = TestFile.Create(TestImages.Gif.Rings);

        int length = testFile.Bytes.Length - 2;

        fixed (byte* data = testFile.Bytes.AsSpan(0, length))
        {
            using UnmanagedMemoryStream stream = new(data, length);
            using Image<Rgba32> image = GifDecoder.Instance.Decode<Rgba32>(DecoderOptions.Default, stream);
            Assert.Equal((200, 200), (image.Width, image.Height));
        }
    }

    [Theory]
    [WithFile(TestImages.Gif.Trans, TestPixelTypes)]
    public void GifDecoder_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    [Theory]
    [WithFile(TestImages.Gif.M4nb, PixelTypes.Rgba32, 5)]
    [WithFile(TestImages.Gif.Rings, PixelTypes.Rgba32, 1)]
    [WithFile(TestImages.Gif.MixedDisposal, PixelTypes.Rgba32, 11)]
    public void Decode_VerifyRootFrameAndFrameCount<TPixel>(TestImageProvider<TPixel> provider, int expectedFrameCount)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        Assert.Equal(expectedFrameCount, image.Frames.Count);
        image.DebugSave(provider);
        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    [Theory]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
    public void CanDecodeJustOneFrame<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        DecoderOptions options = new(Configuration.Default) { MaxFrames = 1 };
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, options);
        Assert.Equal(1, image.Frames.Count);
    }

    [Theory]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
    public void CanDecodeAllFrames<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance);
        Assert.True(image.Frames.Count > 1);
    }

    [Theory]
    [InlineData(TestImages.Gif.Giphy, 8)]
    [InlineData(TestImages.Gif.Rings, 8)]
    [InlineData(TestImages.Gif.Trans, 8)]
    public void DetectPixelSize(string imagePath, int expectedPixelSize)
    {
        TestFile testFile = TestFile.Create(imagePath);
        using MemoryStream stream = new(testFile.Bytes, false);

        ImageInfo imageInfo = Image.Identify(stream);

        Assert.NotNull(imageInfo);
        Assert.Equal(expectedPixelSize, imageInfo.PixelType.BitsPerPixel);
    }

    [Theory]
    [WithFile(TestImages.Gif.ZeroSize, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.ZeroWidth, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.ZeroHeight, PixelTypes.Rgba32)]
    public void Decode_WithInvalidDimensions_DoesThrowException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Exception ex = Record.Exception(
            () =>
            {
                using Image<TPixel> image = provider.GetImage(GifDecoder.Instance);
            });
        Assert.NotNull(ex);
        Assert.Contains("Width or height should not be 0", ex.Message);
    }

    [Theory]
    [WithFile(TestImages.Gif.MaxWidth, PixelTypes.Rgba32, 65535, 1)]
    [WithFile(TestImages.Gif.MaxHeight, PixelTypes.Rgba32, 1, 65535)]
    public void Decode_WithMaxDimensions_Works<TPixel>(TestImageProvider<TPixel> provider, int expectedWidth, int expectedHeight)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance);
        Assert.Equal(expectedWidth, image.Width);
        Assert.Equal(expectedHeight, image.Height);
    }

    [Fact]
    public void CanDecodeIntermingledImages()
    {
        using (Image<Rgba32> kumin1 = Image.Load<Rgba32>(TestFile.Create(TestImages.Gif.Kumin).Bytes))
        using (Image.Load(TestFile.Create(TestImages.Png.Icon).Bytes))
        using (Image<Rgba32> kumin2 = Image.Load<Rgba32>(TestFile.Create(TestImages.Gif.Kumin).Bytes))
        {
            for (int i = 0; i < kumin1.Frames.Count; i++)
            {
                ImageFrame<Rgba32> first = kumin1.Frames[i];
                ImageFrame<Rgba32> second = kumin2.Frames[i];

                Assert.True(second.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> secondMemory));

                first.ComparePixelBufferTo(secondMemory.Span);
            }
        }
    }

    // https://github.com/SixLabors/ImageSharp/issues/1530
    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue1530, PixelTypes.Rgba32)]
    public void Issue1530_BadDescriptorDimensions<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSaveMultiFrame(provider);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2758
    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2758, PixelTypes.Rgba32)]
    public void Issue2758_BadDescriptorDimensions<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSaveMultiFrame(provider);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
    }

    // https://github.com/SixLabors/ImageSharp/issues/405
    [Theory]
    [WithFile(TestImages.Gif.Issues.BadAppExtLength, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.Issues.BadAppExtLength_2, PixelTypes.Rgba32)]
    public void Issue405_BadApplicationExtensionBlockLength<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new DecoderOptions(Configuration.Default) { MaxFrames = 1 });
        image.DebugSave(provider);

        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/1668
    [Theory]
    [WithFile(TestImages.Gif.Issues.InvalidColorIndex, PixelTypes.Rgba32)]
    public void Issue1668_InvalidColorIndex<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new DecoderOptions(Configuration.Default) { MaxFrames = 1 });
        image.DebugSave(provider);

        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    [Theory]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.Kumin, PixelTypes.Rgba32)]
    public void GifDecoder_DegenerateMemoryRequest_ShouldTranslateTo_ImageFormatException<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InPixelsSqrt(10);
        InvalidImageContentException ex = Assert.Throws<InvalidImageContentException>(() => provider.GetImage(GifDecoder.Instance));
        Assert.IsType<InvalidMemoryOperationException>(ex.InnerException);
    }

    [Theory]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.Kumin, PixelTypes.Rgba32)]
    public void GifDecoder_CanDecode_WithLimitedAllocatorBufferCapacity(
        TestImageProvider<Rgba32> provider)
    {
        static void RunTest(string providerDump, string nonContiguousBuffersStr)
        {
            TestImageProvider<Rgba32> provider
                = BasicSerializer.Deserialize<TestImageProvider<Rgba32>>(providerDump);

            provider.LimitAllocatorBufferCapacity().InPixelsSqrt(100);

            using Image<Rgba32> image = provider.GetImage(GifDecoder.Instance);
            image.DebugSave(provider, nonContiguousBuffersStr);
            image.CompareToOriginal(provider);
        }

        string providerDump = BasicSerializer.Serialize(provider);
        RemoteExecutor.Invoke(
                RunTest,
                providerDump,
                "Disco")
            .Dispose();
    }

    // https://github.com/SixLabors/ImageSharp/issues/1962
    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue1962NoColorTable, PixelTypes.Rgba32)]
    public void Issue1962<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new DecoderOptions(Configuration.Default) { MaxFrames = 1 });
        image.DebugSave(provider);

        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2012
    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2012EmptyXmp, PixelTypes.Rgba32)]
    public void Issue2012EmptyXmp<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new DecoderOptions(Configuration.Default) { MaxFrames = 1 });

        image.DebugSave(provider);
        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2012
    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2012BadMinCode, PixelTypes.Rgba32)]
    public void Issue2012BadMinCode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSave(provider);
        image.CompareToReferenceOutput(provider);
    }

    // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
    [Theory]
    [WithFile(TestImages.Gif.Issues.DeferredClearCode, PixelTypes.Rgba32)]
    public void IssueDeferredClearCode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();

        image.DebugSave(provider);
        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2743
    [Theory]
    [WithFile(TestImages.Gif.Issues.BadMaxLzwBits, PixelTypes.Rgba32)]
    public void IssueTooLargeLzwBits<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSaveMultiFrame(provider);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2859
    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2859_A, PixelTypes.Rgba32)]
    [WithFile(TestImages.Gif.Issues.Issue2859_B, PixelTypes.Rgba32)]
    public void Issue2859_LZWPixelStackOverflow<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSaveMultiFrame(provider);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2953
    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2953, PixelTypes.Rgba32)]
    public void Issue2953<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // We should throw a InvalidImageContentException when trying to identify or load an invalid GIF file.
        TestFile testFile = TestFile.Create(provider.SourceFileOrDescription);

        Assert.Throws<InvalidImageContentException>(() => Image.Identify(testFile.FullPath));
        Assert.Throws<InvalidImageContentException>(() => Image.Load(testFile.FullPath));

        DecoderOptions options = new(Configuration.Default) { SkipMetadata = true };
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, testFile.FullPath));
        Assert.Throws<InvalidImageContentException>(() => Image.Load(options, testFile.FullPath));
    }

    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2980, PixelTypes.Rgba32)]
    public void Issue2980<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.DebugSaveMultiFrame(provider);
        image.CompareToReferenceOutputMultiFrame(provider, ImageComparer.Exact);
    }

    [Fact]
    public void Identify_MalformedApplicationExtension_IgnoresNonCriticalErrorsByDefault()
    {
        ImageInfo info = Image.Identify(CreateGifWithMalformedApplicationExtension());
        Assert.Equal(1, info.Width);
        Assert.Equal(1, info.Height);
    }

    [Fact]
    public void Decode_MalformedApplicationExtension_IgnoresNonCriticalErrorsByDefault()
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(CreateGifWithMalformedApplicationExtension());
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
    }

    [Fact]
    public void Identify_MalformedApplicationExtension_ThrowsWithStrict()
    {
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, CreateGifWithMalformedApplicationExtension()));
    }

    [Fact]
    public void Decode_MalformedApplicationExtension_ThrowsWithStrict()
    {
        DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, CreateGifWithMalformedApplicationExtension());
        });
    }

    /// <summary>
    /// Invalid LZW table codes fail unless image data errors are explicitly ignored.
    /// Recovery retains completed rows and continues to the following animation frame.
    /// </summary>
    /// <param name="path">The stored GIF with an invalid first code or an invalid code after a clear.</param>
    /// <param name="completedRows">The number of valid rows before the invalid code.</param>
    /// <param name="frameCount">The number of stored animation frames.</param>
    [Theory]
    [InlineData(TestImages.Gif.LzwCodeOutsideTable, 0, 1)]
    [InlineData(TestImages.Gif.LzwCodeOutsideTableHiddenRow, 1, 2)]
    public void Decode_LzwCodePastCodeTables_RespectsIntegrityHandling(string path, int completedRows, int frameCount)
    {
        byte[] data = TestFile.Create(path).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            if (integrity is not SegmentIntegrityHandling.IgnoreImageData)
            {
                Assert.Throws<InvalidImageContentException>(() =>
                {
                    using Image<Rgba32> image = Image.Load<Rgba32>(options, data);
                });

                continue;
            }

            using Image<Rgba32> actual = Image.Load<Rgba32>(options, data);
            Assert.Equal(new Size(4, frameCount == 1 ? 4 : 2), actual.Size);
            Assert.Equal(frameCount, actual.Frames.Count);

            for (int y = 0; y < actual.Height; y++)
            {
                for (int x = 0; x < actual.Width; x++)
                {
                    // A completed row is red. An undecoded row keeps the frame's transparent background.
                    Assert.Equal(y < completedRows ? new Rgba32(255, 0, 0) : default, actual[x, y]);

                    if (frameCount == 2)
                    {
                        // The complete green frame after the corrupt frame proves that sub-block recovery resumed correctly.
                        Assert.Equal(new Rgba32(0, 255, 0), actual.Frames[1][x, y]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Identify uses LZW index skipping and must apply the same image data policy as full decoding.
    /// </summary>
    /// <param name="path">The stored GIF with an invalid table code.</param>
    /// <param name="frameCount">The number of stored animation frames.</param>
    [Theory]
    [InlineData(TestImages.Gif.LzwCodeOutsideTable, 1)]
    [InlineData(TestImages.Gif.LzwCodeOutsideTableHiddenRow, 2)]
    public void Identify_LzwCodePastCodeTables_RespectsIntegrityHandling(string path, int frameCount)
    {
        byte[] data = TestFile.Create(path).Bytes;

        foreach (SegmentIntegrityHandling integrity in Enum.GetValues<SegmentIntegrityHandling>())
        {
            DecoderOptions options = new(Configuration.Default) { SegmentIntegrityHandling = integrity };
            if (integrity is not SegmentIntegrityHandling.IgnoreImageData)
            {
                Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, data));
                continue;
            }

            ImageInfo info = Image.Identify(options, data);
            Assert.Equal(new Size(4, frameCount == 1 ? 4 : 2), info.Size);
            Assert.Equal(frameCount, info.FrameMetadataCollection.Count);
        }
    }

    /// <summary>
    /// Clips stored frame rows to the logical screen without changing interlace order or later frame parsing.
    /// </summary>
    /// <param name="path">The stored GIF with a four-color row pattern and a second frame containing one white pixel.</param>
    /// <param name="width">The first frame's stored width.</param>
    /// <param name="height">The first frame's stored height.</param>
    /// <param name="left">The first frame's left offset.</param>
    /// <param name="top">The first frame's top offset.</param>
    [Theory]
    [InlineData(TestImages.Gif.Frame20x20At0x0Interlaced, 20, 20, 0, 0)]
    [InlineData(TestImages.Gif.Frame20x20At0x0Sequential, 20, 20, 0, 0)]
    [InlineData(TestImages.Gif.Frame10x10At0x5Interlaced, 10, 10, 0, 5)]
    [InlineData(TestImages.Gif.Frame10x10At0x5Sequential, 10, 10, 0, 5)]
    [InlineData(TestImages.Gif.Frame10x10At5x3Interlaced, 10, 10, 5, 3)]
    [InlineData(TestImages.Gif.Frame10x10At0x10Interlaced, 10, 10, 0, 10)]
    [InlineData(TestImages.Gif.Frame10x10At0x10Sequential, 10, 10, 0, 10)]
    [InlineData(TestImages.Gif.Frame10x20At0x8Interlaced, 10, 20, 0, 8)]
    [InlineData(TestImages.Gif.Frame10x20At0x9Interlaced, 10, 20, 0, 9)]
    [InlineData(TestImages.Gif.Frame10x1At0x0Interlaced, 10, 1, 0, 0)]
    [InlineData(TestImages.Gif.Frame10x2At0x0Interlaced, 10, 2, 0, 0)]
    [InlineData(TestImages.Gif.Frame10x3At0x7Interlaced, 10, 3, 0, 7)]
    [InlineData(TestImages.Gif.Frame10x4At0x0Interlaced, 10, 4, 0, 0)]
    [InlineData(TestImages.Gif.Frame4x4At12x0Interlaced, 4, 4, 12, 0)]
    [InlineData(TestImages.Gif.Frame4x4At12x0Sequential, 4, 4, 12, 0)]
    public void Decode_FrameExceedingLogicalScreen_WritesOnlyRowsInsideCanvas(string path, int width, int height, int left, int top)
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(TestFile.Create(path).Bytes);

        Assert.Equal(new Size(10, 10), image.Size);
        Assert.Equal(2, image.Frames.Count);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                // Frame row n contains palette index n modulo four. Pixels outside its rectangle stay transparent.
                bool inFrame = x >= left && x < left + width && y >= top && y < top + height;
                Rgba32 expected = inFrame ? GeometryTestColorTable[(y - top) % 4] : default;
                Assert.Equal(expected, image[x, y]);
            }
        }

        // Even when the first frame is clipped or fully hidden, its data must not consume the next image descriptor.
        Assert.Equal(new Rgba32(255, 255, 255), image.Frames[1][0, 0]);
    }

    private static byte[] CreateGifWithMalformedApplicationExtension()
    {
        // The application extension declares a 5-byte application block (0x05), but GIF application
        // extensions require the fixed identifier/authentication block to be 11 bytes long.
        return
        [
            (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a',
            0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00,
            0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF,
            0x21, 0xFF, 0x05, (byte)'B', (byte)'a', (byte)'d', (byte)'A', (byte)'p', 0x00,
            0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
            0x02, 0x02, 0x44, 0x01, 0x00,
            0x3B
        ];
    }
}
