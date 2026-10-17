// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics.X86;
using Microsoft.DotNet.RemoteExecutor;
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
    /// The global color table of the synthesized frame geometry test images.
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
        DecoderOptions options = new()
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
        DecoderOptions options = new() { MaxFrames = 1 };
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
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new DecoderOptions { MaxFrames = 1 });
        image.DebugSave(provider);

        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/1668
    [Theory]
    [WithFile(TestImages.Gif.Issues.InvalidColorIndex, PixelTypes.Rgba32)]
    public void Issue1668_InvalidColorIndex<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new DecoderOptions { MaxFrames = 1 });
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
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new DecoderOptions { MaxFrames = 1 });
        image.DebugSave(provider);

        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2012
    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2012EmptyXmp, PixelTypes.Rgba32)]
    public void Issue2012EmptyXmp<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new DecoderOptions { MaxFrames = 1 });

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

        DecoderOptions options = new() { SkipMetadata = true };
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
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() => Image.Identify(options, CreateGifWithMalformedApplicationExtension()));
    }

    [Fact]
    public void Decode_MalformedApplicationExtension_ThrowsWithStrict()
    {
        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };
        Assert.Throws<InvalidImageContentException>(() =>
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(options, CreateGifWithMalformedApplicationExtension());
        });
    }

    [Fact]
    public void Decode_LzwCodePastCodeTables_StopsDecodingFrame()
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(CreateLzwCodePastCodeTablesGif());

        // No index is decoded so every pixel keeps the cleared index 0.
        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Assert.Equal(GeometryTestColorTable[0], image[x, y]);
            }
        }
    }

    [Fact]
    public void Identify_LzwCodePastCodeTables_ReturnsImageInfo()
    {
        // Identify skips the frame indices so this exercises the index skipping path of the LZW decoder.
        ImageInfo info = Image.Identify(CreateLzwCodePastCodeTablesGif());

        Assert.Equal(4, info.Width);
        Assert.Equal(4, info.Height);
    }

    [Theory]
    [InlineData(20, 20, 0, 0, true)]
    [InlineData(20, 20, 0, 0, false)]
    [InlineData(10, 10, 0, 5, true)]
    [InlineData(10, 10, 0, 5, false)]
    [InlineData(10, 10, 5, 3, true)]
    [InlineData(10, 10, 0, 10, true)]
    [InlineData(10, 10, 0, 10, false)]
    [InlineData(10, 20, 0, 8, true)]
    [InlineData(10, 20, 0, 9, true)]
    [InlineData(10, 1, 0, 0, true)]
    [InlineData(10, 2, 0, 0, true)]
    [InlineData(10, 3, 0, 7, true)]
    [InlineData(10, 4, 0, 0, true)]
    [InlineData(4, 4, 12, 0, true)]
    [InlineData(4, 4, 12, 0, false)]
    public void Decode_FrameExceedingLogicalScreen_WritesOnlyRowsInsideCanvas(int width, int height, int left, int top, bool interlaced)
    {
        const int screenSize = 10;

        // Each frame row is filled with the color index of its frame row number modulo 4,
        // stored in the order an interlaced or sequential frame stores its rows.
        int[] rowOrder = interlaced ? GetInterlacedRowOrder(height) : Enumerable.Range(0, height).ToArray();
        using Buffer2D<byte> indices = Configuration.Default.MemoryAllocator.Allocate2D<byte>(width, height);
        for (int i = 0; i < rowOrder.Length; i++)
        {
            indices.DangerousGetRowSpan(i).Fill((byte)(rowOrder[i] % 4));
        }

        using MemoryStream imageData = new();
        using (LzwEncoder encoder = new(Configuration.Default.MemoryAllocator, 2))
        {
            encoder.Encode(indices, imageData);
        }

        byte[] data = CreateGeometryTestGif(screenSize, screenSize, left, top, width, height, interlaced, imageData.ToArray());

        using Image<Rgba32> image = Image.Load<Rgba32>(data);

        Assert.Equal(screenSize, image.Width);
        Assert.Equal(screenSize, image.Height);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                // Pixels outside the frame are never written and stay transparent.
                bool inFrame = x >= left && x < left + width && y >= top && y < top + height;
                Rgba32 expected = inFrame ? GeometryTestColorTable[(y - top) % 4] : default;
                Assert.Equal(expected, image[x, y]);
            }
        }
    }

    /// <summary>
    /// Creates a 4x4 GIF whose frame uses a minimum code size of 12 and holds the single 13-bit code 4098.
    /// The clear code is 4096, so the code lies past the 4096-entry LZW code tables.
    /// </summary>
    /// <returns>The encoded GIF.</returns>
    private static byte[] CreateLzwCodePastCodeTablesGif()
        => CreateGeometryTestGif(4, 4, 0, 0, 4, 4, false, [12, 0x02, 0x02, 0x10, 0x00]);

    /// <summary>
    /// Creates a GIF89a with the 4-entry <see cref="GeometryTestColorTable"/> as its global color table
    /// and a single image descriptor without a local color table.
    /// </summary>
    /// <param name="screenWidth">The logical screen width.</param>
    /// <param name="screenHeight">The logical screen height.</param>
    /// <param name="left">The frame left position.</param>
    /// <param name="top">The frame top position.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <param name="interlaced">Whether the frame is interlaced.</param>
    /// <param name="imageData">The image data, from the LZW minimum code size to the block terminator.</param>
    /// <returns>The encoded GIF.</returns>
    private static byte[] CreateGeometryTestGif(
        int screenWidth,
        int screenHeight,
        int left,
        int top,
        int width,
        int height,
        bool interlaced,
        byte[] imageData)
    {
        using MemoryStream stream = new();
        stream.Write("GIF89a"u8);

        // Logical screen descriptor flagging a 4-entry global color table.
        WriteUInt16(stream, screenWidth);
        WriteUInt16(stream, screenHeight);
        stream.Write([0x81, 0x00, 0x00]);
        foreach (Rgba32 color in GeometryTestColorTable)
        {
            stream.Write([color.R, color.G, color.B]);
        }

        // Image descriptor without a local color table.
        stream.WriteByte(GifConstants.ImageLabel);
        WriteUInt16(stream, left);
        WriteUInt16(stream, top);
        WriteUInt16(stream, width);
        WriteUInt16(stream, height);
        stream.WriteByte((byte)(interlaced ? 0x40 : 0x00));

        stream.Write(imageData);
        stream.WriteByte(GifConstants.EndIntroducer);
        return stream.ToArray();
    }

    /// <summary>
    /// Gets the frame rows in the order an interlaced GIF stores them.
    /// </summary>
    /// <param name="height">The frame height.</param>
    /// <returns>The frame row numbers in storage order.</returns>
    private static int[] GetInterlacedRowOrder(int height)
    {
        List<int> rows = [];
        foreach ((int start, int step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
        {
            for (int y = start; y < height; y += step)
            {
                rows.Add(y);
            }
        }

        return [.. rows];
    }

    /// <summary>
    /// Writes a little-endian 16-bit value.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="value">The value to write.</param>
    private static void WriteUInt16(Stream stream, int value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
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
