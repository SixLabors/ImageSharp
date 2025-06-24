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
    {
        TestImages.Gif.Giphy, TestImages.Gif.Kumin
    };

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
            TargetSize = new() { Width = 150, Height = 150 },
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
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new() { MaxFrames = 1 });
        image.DebugSave(provider);

        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/1668
    [Theory]
    [WithFile(TestImages.Gif.Issues.InvalidColorIndex, PixelTypes.Rgba32)]
    public void Issue1668_InvalidColorIndex<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new() { MaxFrames = 1 });
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
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new() { MaxFrames = 1 });
        image.DebugSave(provider);

        image.CompareFirstFrameToReferenceOutput(ImageComparer.Exact, provider);
    }

    // https://github.com/SixLabors/ImageSharp/issues/2012
    [Theory]
    [WithFile(TestImages.Gif.Issues.Issue2012EmptyXmp, PixelTypes.Rgba32)]
    public void Issue2012EmptyXmp<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage(GifDecoder.Instance, new() { MaxFrames = 1 });

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
}
