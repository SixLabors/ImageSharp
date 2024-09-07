// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;

[Trait("Category", "Processors")]
public class ResizeTests
{
    private const PixelTypes CommonNonDefaultPixelTypes =
        PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Bgr24 | PixelTypes.RgbaVector;

    public static readonly string[] AllResamplerNames = TestUtils.GetAllResamplerNames();

    public static readonly string[] CommonTestImages = { TestImages.Png.CalliphoraPartial };

    public static readonly string[] SmokeTestResamplerNames =
        {
            nameof(KnownResamplers.NearestNeighbor),
            nameof(KnownResamplers.Bicubic),
            nameof(KnownResamplers.Box),
            nameof(KnownResamplers.Lanczos5),
        };

    private static readonly ImageComparer ValidatorComparer =
        ImageComparer.TolerantPercentage(0.07F);

    [Fact]
    public void Resize_PixelAgnostic()
    {
        string filePath = TestFile.GetInputFileFullPath(TestImages.Jpeg.Baseline.Calliphora);

        using Image image = Image.Load(filePath);
        image.Mutate(x => x.Resize(image.Size / 2));
        string path = Path.Combine(
            TestEnvironment.CreateOutputDirectory(nameof(ResizeTests)),
            nameof(this.Resize_PixelAgnostic) + ".png");

        image.Save(path);
    }

    [Theory(Skip = "Debug only, enable manually")]
    [WithTestPatternImages(4000, 4000, PixelTypes.Rgba32, 300, 1024)]
    [WithTestPatternImages(3032, 3032, PixelTypes.Rgba32, 400, 1024)]
    [WithTestPatternImages(3032, 3032, PixelTypes.Rgba32, 400, 128)]
    public void LargeImage<TPixel>(TestImageProvider<TPixel> provider, int destSize, int workingBufferSizeHintInKilobytes)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!TestEnvironment.Is64BitProcess)
        {
            return;
        }

        provider.Configuration.WorkingBufferSizeHintInBytes = workingBufferSizeHintInKilobytes * 1024;

        using Image<TPixel> image = provider.GetImage();
        image.Mutate(x => x.Resize(destSize, destSize));
        image.DebugSave(provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithBasicTestPatternImages(15, 12, PixelTypes.Rgba32, 2, 3, 1, 2)]
    [WithBasicTestPatternImages(2, 256, PixelTypes.Rgba32, 1, 1, 1, 8)]
    [WithBasicTestPatternImages(2, 32, PixelTypes.Rgba32, 1, 1, 1, 2)]
    public void Resize_BasicSmall<TPixel>(TestImageProvider<TPixel> provider, int wN, int wD, int hN, int hD)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Basic test case, very helpful for debugging
        // [WithBasicTestPatternImages(15, 12, PixelTypes.Rgba32, 2, 3, 1, 2)] means:
        // resizing: (15, 12) -> (10, 6)
        // kernel dimensions: (3, 4)
        using Image<TPixel> image = provider.GetImage();
        Size destSize = new(image.Width * wN / wD, image.Height * hN / hD);
        image.Mutate(x => x.Resize(destSize, KnownResamplers.Bicubic, false));
        FormattableString outputInfo = $"({wN}÷{wD},{hN}÷{hD})";
        image.DebugSave(provider, outputInfo, appendPixelTypeToFileName: false);
        image.CompareToReferenceOutput(provider, outputInfo, appendPixelTypeToFileName: false);
    }

    private static readonly int SizeOfVector4 = Unsafe.SizeOf<Vector4>();

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 50)]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 60)]
    [WithTestPatternImages(100, 400, PixelTypes.Rgba32, 110)]
    [WithTestPatternImages(79, 97, PixelTypes.Rgba32, 73)]
    [WithTestPatternImages(79, 97, PixelTypes.Rgba32, 5)]
    [WithTestPatternImages(47, 193, PixelTypes.Rgba32, 73)]
    [WithTestPatternImages(23, 211, PixelTypes.Rgba32, 31)]
    public void WorkingBufferSizeHintInBytes_IsAppliedCorrectly<TPixel>(
        TestImageProvider<TPixel> provider,
        int workingBufferLimitInRows)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image0 = provider.GetImage();
        Size destSize = image0.Size / 4;

        Configuration configuration = Configuration.CreateDefaultInstance();

        int workingBufferSizeHintInBytes = workingBufferLimitInRows * destSize.Width * SizeOfVector4;
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        configuration.MemoryAllocator = allocator;
        configuration.WorkingBufferSizeHintInBytes = workingBufferSizeHintInBytes;

        ResizeKernelMap verticalKernelMap = ResizeKernelMap.Calculate<BicubicResampler>(
            default,
            destSize.Height,
            image0.Height,
            Configuration.Default.MemoryAllocator);
        int minimumWorkerAllocationInBytes = verticalKernelMap.MaxDiameter * 2 * destSize.Width * SizeOfVector4;
        verticalKernelMap.Dispose();

        using Image<TPixel> image = image0.Clone(configuration);
        image.Mutate(x => x.Resize(destSize, KnownResamplers.Bicubic, false));

        image.DebugSave(
            provider,
            testOutputDetails: workingBufferLimitInRows,
            appendPixelTypeToFileName: false);
        image.CompareToReferenceOutput(
            ImageComparer.TolerantPercentage(0.004f),
            provider,
            testOutputDetails: workingBufferLimitInRows,
            appendPixelTypeToFileName: false);

        Assert.NotEmpty(allocator.AllocationLog);

        int maxAllocationSize = allocator.AllocationLog.Where(
            e => e.ElementType == typeof(Vector4)).Max(e => e.LengthInBytes);

        Assert.True(maxAllocationSize <= Math.Max(workingBufferSizeHintInBytes, minimumWorkerAllocationInBytes));
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32, 100, 100)]
    [WithTestPatternImages(200, 200, PixelTypes.Rgba32, 31, 73)]
    [WithTestPatternImages(200, 200, PixelTypes.Rgba32, 73, 31)]
    [WithTestPatternImages(200, 193, PixelTypes.Rgba32, 13, 17)]
    [WithTestPatternImages(200, 193, PixelTypes.Rgba32, 79, 23)]
    [WithTestPatternImages(200, 503, PixelTypes.Rgba32, 61, 33)]
    public void WorksWithDiscoBuffers<TPixel>(
        TestImageProvider<TPixel> provider,
        int workingBufferLimitInRows,
        int bufferCapacityInRows)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> expected = provider.GetImage();
        int width = expected.Width;
        Size destSize = expected.Size / 4;
        expected.Mutate(c => c.Resize(destSize, KnownResamplers.Bicubic, false));

        // Replace configuration:
        provider.Configuration = Configuration.CreateDefaultInstance();

        // Note: when AllocatorCapacityInBytes < WorkingBufferSizeHintInBytes,
        // ResizeProcessor is expected to use the minimum of the two values, when establishing the working buffer.
        provider.LimitAllocatorBufferCapacity().InBytes(width * bufferCapacityInRows * SizeOfVector4);
        provider.Configuration.WorkingBufferSizeHintInBytes = width * workingBufferLimitInRows * SizeOfVector4;

        using Image<TPixel> actual = provider.GetImage();
        actual.Mutate(c => c.Resize(destSize, KnownResamplers.Bicubic, false));
        actual.DebugSave(provider, $"{workingBufferLimitInRows}-{bufferCapacityInRows}");

        ImageComparer.Exact.VerifySimilarity(expected, actual);
    }

    [Theory]
    [WithTestPatternImages(100, 100, PixelTypes.Rgba32)]
    public void Resize_Compand<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.Mutate(x => x.Resize(image.Size / 2, true));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider);
    }

    [Theory]
    [WithFile(TestImages.Png.Kaboom, PixelTypes.Rgba32, false)]
    [WithFile(TestImages.Png.Kaboom, PixelTypes.Rgba32, true)]
    public void Resize_DoesNotBleedAlphaPixels<TPixel>(TestImageProvider<TPixel> provider, bool compand)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        string details = compand ? "Compand" : string.Empty;

        provider.RunValidatingProcessorTest(
            x => x.Resize(x.GetCurrentSize() / 2, compand),
            details,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithFile(TestImages.Png.Kaboom, PixelTypes.Rgba32, false)]
    [WithFile(TestImages.Png.Kaboom, PixelTypes.Rgba32, true)]
    public void Resize_PremultiplyAlpha<TPixel>(TestImageProvider<TPixel> provider, bool premultiplyAlpha)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        string details = premultiplyAlpha ? "On" : "Off";

        provider.RunValidatingProcessorTest(
            x =>
            {
                ResizeOptions resizeOptions = new()
                {
                    Size = x.GetCurrentSize() / 2,
                    Mode = ResizeMode.Crop,
                    Sampler = KnownResamplers.Bicubic,
                    Compand = false,
                    PremultiplyAlpha = premultiplyAlpha
                };
                x.Resize(resizeOptions);
            },
            details,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithFile(TestImages.Gif.Giphy, PixelTypes.Rgba32)]
    public void Resize_IsAppliedToAllFrames<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.Mutate(x => x.Resize(image.Width / 2, image.Height / 2, KnownResamplers.Bicubic));

        // Comparer fights decoder with gif-s. Could not use CompareToReferenceOutput here :(
        image.DebugSave(provider, extension: "gif");
    }

    [Theory]
    [WithTestPatternImages(50, 50, CommonNonDefaultPixelTypes)]
    public void Resize_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.RunValidatingProcessorTest(x => x.Resize(x.GetCurrentSize() / 2), comparer: ValidatorComparer);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32)]
    public void Resize_ThrowsForWrappedMemoryImage<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image0 = provider.GetImage();
        Assert.True(image0.DangerousTryGetSinglePixelMemory(out Memory<TPixel> imageMem));
        TestMemoryManager<TPixel> mmg = TestMemoryManager<TPixel>.CreateAsCopyOf(imageMem.Span);

        using Image<TPixel> image1 = Image.WrapMemory(mmg.Memory, image0.Width, image0.Height);
        Assert.ThrowsAny<Exception>(
            () => { image1.Mutate(x => x.Resize(image0.Width / 2, image0.Height / 2, true)); });
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24, 1)]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24, 4)]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24, 8)]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24, -1)]
    public void Resize_WorksWithAllParallelismLevels<TPixel>(
        TestImageProvider<TPixel> provider,
        int maxDegreeOfParallelism)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.Configuration.MaxDegreeOfParallelism =
            maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : Environment.ProcessorCount;

        FormattableString details = $"MDP{maxDegreeOfParallelism}";

        provider.RunValidatingProcessorTest(
            x => x.Resize(x.GetCurrentSize() / 2),
            details,
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), nameof(AllResamplerNames), PixelTypes.Rgba32 | PixelTypes.Rgb24, 0.5f, null, null)]
    [WithFileCollection(
        nameof(CommonTestImages),
        nameof(SmokeTestResamplerNames),
        PixelTypes.Rgba32 | PixelTypes.Rgb24,
        0.3f,
        null,
        null)]
    [WithFileCollection(
        nameof(CommonTestImages),
        nameof(SmokeTestResamplerNames),
        PixelTypes.Rgba32 | PixelTypes.Rgb24,
        1.8f,
        null,
        null)]
    [WithTestPatternImages(nameof(SmokeTestResamplerNames), 100, 100, PixelTypes.Rgba32, 0.5f, null, null)]
    [WithTestPatternImages(nameof(SmokeTestResamplerNames), 100, 100, PixelTypes.Rgba32, 1f, null, null)]
    [WithTestPatternImages(nameof(SmokeTestResamplerNames), 50, 50, PixelTypes.Rgba32, 8f, null, null)]
    [WithTestPatternImages(nameof(SmokeTestResamplerNames), 201, 199, PixelTypes.Rgba32, null, 100, 99)]
    [WithTestPatternImages(nameof(SmokeTestResamplerNames), 301, 1180, PixelTypes.Rgba32, null, 300, 480)]
    [WithTestPatternImages(nameof(SmokeTestResamplerNames), 49, 80, PixelTypes.Rgba32, null, 301, 100)]
    public void Resize_WorksWithAllResamplers<TPixel>(
        TestImageProvider<TPixel> provider,
        string samplerName,
        float? ratio,
        int? specificDestWidth,
        int? specificDestHeight)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        IResampler sampler = TestUtils.GetResampler(samplerName);

        // NearestNeighbourResampler is producing slightly different results With classic .NET framework on 32bit
        // most likely because of differences in numeric behavior.
        // The difference is well visible when comparing output for
        // Resize_WorksWithAllResamplers_TestPattern301x1180_NearestNeighbor-300x480.png
        // TODO: Should we investigate this?
        bool allowHigherInaccuracy = !TestEnvironment.Is64BitProcess
                                     && TestEnvironment.NetCoreVersion == null
                                     && sampler is NearestNeighborResampler;

        ImageComparer comparer = ImageComparer.TolerantPercentage(allowHigherInaccuracy ? 0.3f : 0.017f);

        // Let's make the working buffer size non-default:
        provider.Configuration.WorkingBufferSizeHintInBytes = 16 * 1024 * SizeOfVector4;

        provider.RunValidatingProcessorTest(
            ctx =>
            {
                SizeF newSize;
                string destSizeInfo;
                if (ratio.HasValue)
                {
                    newSize = ctx.GetCurrentSize() * ratio.Value;
                    destSizeInfo = ratio.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    if (!specificDestWidth.HasValue || !specificDestHeight.HasValue)
                    {
                        throw new InvalidOperationException(
                            "invalid dimensional input for Resize_WorksWithAllResamplers!");
                    }

                    newSize = new(specificDestWidth.Value, specificDestHeight.Value);
                    destSizeInfo = $"{newSize.Width}x{newSize.Height}";
                }

                FormattableString testOutputDetails = $"{samplerName}-{destSizeInfo}";

                ctx.Resize((Size)newSize, sampler, false);
                return testOutputDetails;
            },
            comparer,
            appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeFromSourceRectangle<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        Rectangle sourceRectangle = new(
            image.Width / 8,
            image.Height / 8,
            image.Width / 4,
            image.Height / 4);
        Rectangle destRectangle = new(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);

        image.Mutate(
            x => x.Resize(
                image.Width,
                image.Height,
                KnownResamplers.Bicubic,
                sourceRectangle,
                destRectangle,
                false));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeHeightAndKeepAspect<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.Mutate(x => x.Resize(0, image.Height / 3, false));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithTestPatternImages(10, 100, PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeHeightCannotKeepAspectKeepsOnePixel<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.Mutate(x => x.Resize(0, 5));
        Assert.Equal(1, image.Width);
        Assert.Equal(5, image.Height);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeWidthAndKeepAspect<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.Mutate(x => x.Resize(image.Width / 3, 0, false));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithTestPatternImages(100, 10, PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeWidthCannotKeepAspectKeepsOnePixel<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        image.Mutate(x => x.Resize(5, 0));
        Assert.Equal(5, image.Width);
        Assert.Equal(1, image.Height);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeWithBoxPadMode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ResizeOptions options = new()
        {
            Size = new(image.Width + 200, image.Height + 200),
            Mode = ResizeMode.BoxPad,
            PadColor = Color.HotPink
        };

        image.Mutate(x => x.Resize(options));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeWithCropHeightMode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ResizeOptions options = new() { Size = new(image.Width, image.Height / 2) };

        image.Mutate(x => x.Resize(options));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeWithCropWidthMode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ResizeOptions options = new() { Size = new(image.Width / 2, image.Height) };

        image.Mutate(x => x.Resize(options));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Issues.IncorrectResize1006, PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void CanResizeLargeImageWithCropMode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ResizeOptions options = new()
        {
            Size = new(480, 600),
            Mode = ResizeMode.Crop
        };

        image.Mutate(x => x.Resize(options));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeWithMaxMode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ResizeOptions options = new() { Size = new(300, 300), Mode = ResizeMode.Max };

        image.Mutate(x => x.Resize(options));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeWithMinMode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ResizeOptions options = new()
        {
            Size = new((int)Math.Round(image.Width * .75F), (int)Math.Round(image.Height * .95F)),
            Mode = ResizeMode.Min
        };

        image.Mutate(x => x.Resize(options));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeWithPadMode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ResizeOptions options = new()
        {
            Size = new(image.Width + 200, image.Height),
            Mode = ResizeMode.Pad,
            PadColor = Color.Lavender
        };

        image.Mutate(x => x.Resize(options));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFileCollection(nameof(CommonTestImages), PixelTypes.Rgba32 | PixelTypes.Rgb24)]
    public void ResizeWithStretchMode<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> image = provider.GetImage();
        ResizeOptions options = new()
        {
            Size = new(image.Width / 2, image.Height),
            Mode = ResizeMode.Stretch
        };

        image.Mutate(x => x.Resize(options));

        image.DebugSave(provider);
        image.CompareToReferenceOutput(ValidatorComparer, provider, appendPixelTypeToFileName: false);
    }

    [Theory]
    [WithFile(TestImages.Jpeg.Issues.ExifDecodeOutOfRange694, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Issues.ExifGetString750Transform, PixelTypes.Rgb24)]
    [WithFile(TestImages.Jpeg.Issues.ExifResize1049, PixelTypes.Rgb24)]
    public void CanResizeExifIssueImages<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Test images are large so skip on 32bit for now.
        if (!TestEnvironment.Is64BitProcess)
        {
            return;
        }

        using Image<TPixel> image = provider.GetImage();
        // Don't bother saving, we're testing the EXIF metadata updates.
        image.Mutate(x => x.Resize(image.Width / 2, image.Height / 2));
    }

    [Fact]
    public void Issue1195()
    {
        using Image<Rgba32> image = new(2, 300);
        Size size = new(50, 50);
        image.Mutate(x => x
            .Resize(
                new ResizeOptions
                {
                    Size = size,
                    Mode = ResizeMode.Max
                }));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 6)]
    [InlineData(2, 10)]
    [InlineData(8, 1)]
    [InlineData(3, 7)]
    public void Issue1342(int width, int height)
    {
        using Image<Rgba32> image = new(1, 1);
        Size size = new(width, height);
        image.Mutate(x => x
            .Resize(
                new ResizeOptions
                {
                    Size = size,
                    Sampler = KnownResamplers.NearestNeighbor
                }));

        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
    }

    [Theory]
    [WithBasicTestPatternImages(20, 20, PixelTypes.Rgba32)]
    public void Issue1625_LimitedAllocator<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        provider.LimitAllocatorBufferCapacity().InBytes(1000);
        provider.RunValidatingProcessorTest(
            x => x.Resize(30, 30),
            appendPixelTypeToFileName: false,
            appendSourceFileOrDescription: false);
    }
}
