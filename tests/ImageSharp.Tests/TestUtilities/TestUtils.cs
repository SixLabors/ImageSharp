// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// Various utility and extension methods.
/// </summary>
public static class TestUtils
{
    private static readonly Dictionary<Type, PixelTypes> ClrTypes2PixelTypes = new();

    private static readonly Assembly ImageSharpAssembly = typeof(Rgba32).GetTypeInfo().Assembly;

    private static readonly Dictionary<PixelTypes, Type> PixelTypes2ClrTypes = new();

    private static readonly PixelTypes[] AllConcretePixelTypes = GetAllPixelTypes()
        .Except([PixelTypes.Undefined, PixelTypes.All])
        .ToArray();

    static TestUtils()
    {
        // Add Rgba32 Our default.
        Type defaultPixelFormatType = typeof(Rgba32);
        PixelTypes2ClrTypes[PixelTypes.Rgba32] = defaultPixelFormatType;
        ClrTypes2PixelTypes[defaultPixelFormatType] = PixelTypes.Rgba32;

        // Add PixelFormat types
        string nameSpace = typeof(A8).FullName;
        nameSpace = nameSpace.Substring(0, nameSpace.Length - typeof(A8).Name.Length - 1);
        foreach (PixelTypes pt in AllConcretePixelTypes.Where(pt => pt != PixelTypes.Rgba32))
        {
            string typeName = $"{nameSpace}.{pt}";
            Type t = ImageSharpAssembly.GetType(typeName);
            PixelTypes2ClrTypes[pt] = t ?? throw new InvalidOperationException($"Could not find: {typeName}");
            ClrTypes2PixelTypes[t] = pt;
        }
    }

    public static bool HasFlag(this PixelTypes pixelTypes, PixelTypes flag) => (pixelTypes & flag) == flag;

    public static byte[] GetRandomBytes(int length, int seed = 42)
    {
        Random rnd = new(seed);
        byte[] bytes = new byte[length];
        rnd.NextBytes(bytes);
        return bytes;
    }

    internal static byte[] FillImageWithRandomBytes(Image<La16> image)
    {
        byte[] expected = GetRandomBytes(image.Width * image.Height * 2);
        image.ProcessPixelRows(accessor =>
        {
            int cnt = 0;
            for (int y = 0; y < accessor.Height; y++)
            {
                Span<La16> row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = new La16(expected[cnt++], expected[cnt++]);
                }
            }
        });
        return expected;
    }

    public static bool IsEquivalentTo<TPixel>(this Image<TPixel> a, Image<TPixel> b, bool compareAlpha = true)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return false;
        }

        Buffer2D<TPixel> pixA = a.GetRootFramePixelBuffer();
        Buffer2D<TPixel> pixB = b.GetRootFramePixelBuffer();
        for (int y = 0; y < a.Height; y++)
        {
            for (int x = 0; x < a.Width; x++)
            {
                TPixel ca = pixA[x, y];
                TPixel cb = pixB[x, y];

                if (compareAlpha)
                {
                    if (!ca.Equals(cb))
                    {
                        return false;
                    }
                }
                else
                {
                    Rgba32 rgba = ca.ToRgba32();
                    Rgb24 rgb1 = rgba.Rgb;

                    rgba = cb.ToRgba32();
                    Rgb24 rgb2 = rgba.Rgb;

                    if (!rgb1.Equals(rgb2))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    public static string ToCsv<T>(this IEnumerable<T> items, string separator = ",") => string.Join(separator, items.Select(o => string.Format(CultureInfo.InvariantCulture, "{0}", o)));

    public static Type GetClrType(this PixelTypes pixelType) => PixelTypes2ClrTypes[pixelType];

    /// <summary>
    /// Returns the <see cref="PixelTypes"/> enumerations for the given type.
    /// </summary>
    /// <returns>The pixel type.</returns>
    public static PixelTypes GetPixelType(this Type colorStructClrType) => ClrTypes2PixelTypes[colorStructClrType];

    public static IEnumerable<KeyValuePair<PixelTypes, Type>> ExpandAllTypes(this PixelTypes pixelTypes)
    {
        if (pixelTypes == PixelTypes.Undefined)
        {
            return [];
        }
        else if (pixelTypes == PixelTypes.All)
        {
            // TODO: Need to return unknown types here without forcing CLR to load all types in ImageSharp assembly
            return PixelTypes2ClrTypes;
        }

        Dictionary<PixelTypes, Type> result = new();
        foreach (PixelTypes pt in AllConcretePixelTypes)
        {
            if (pixelTypes.HasAll(pt))
            {
                result[pt] = pt.GetClrType();
            }
        }

        return result;
    }

    internal static bool HasAll(this PixelTypes pixelTypes, PixelTypes flagsToCheck) =>
        (pixelTypes & flagsToCheck) == flagsToCheck;

    /// <summary>
    /// Enumerate all available <see cref="PixelTypes"/>-s
    /// </summary>
    /// <returns>The pixel types</returns>
    internal static PixelTypes[] GetAllPixelTypes() => (PixelTypes[])Enum.GetValues(typeof(PixelTypes));

    internal static Color GetColorByName(string colorName)
    {
        FieldInfo f = (FieldInfo)typeof(Color).GetMember(colorName)[0];
        return (Color)f.GetValue(null);
    }

    internal static TPixel GetPixelOfNamedColor<TPixel>(string colorName)
        where TPixel : unmanaged, IPixel<TPixel> =>
        GetColorByName(colorName).ToPixel<TPixel>();

    internal static void RunBufferCapacityLimitProcessorTest<TPixel>(
        this TestImageProvider<TPixel> provider,
        int bufferCapacityInPixelRows,
        Action<IImageProcessingContext> process,
        object testOutputDetails = null,
        ImageComparer comparer = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        comparer ??= ImageComparer.Exact;
        using Image<TPixel> expected = provider.GetImage();
        int width = expected.Width;
        expected.Mutate(process);

        TestMemoryAllocator allocator = new();
        provider.Configuration.MemoryAllocator = allocator;
        allocator.BufferCapacityInBytes = bufferCapacityInPixelRows * width * Unsafe.SizeOf<TPixel>();

        using Image<TPixel> actual = provider.GetImage();
        actual.Mutate(process);
        comparer.VerifySimilarity(expected, actual);
    }

    /// <summary>
    /// Utility for testing image processor extension methods:
    /// 1. Run a processor defined by 'process'
    /// 2. Run 'DebugSave()' to save the output locally
    /// 3. Run 'CompareToReferenceOutput()' to compare the results to the expected output
    /// </summary>
    /// <param name="provider">The <see cref="TestImageProvider{TPixel}"/></param>
    /// <param name="process">The image processing method to test. (As a delegate)</param>
    /// <param name="testOutputDetails">The value to append to the test output.</param>
    /// <param name="comparer">The custom image comparer to use</param>
    /// <param name="appendPixelTypeToFileName">If true, the pixel type will by appended to the output file.</param>
    /// <param name="appendSourceFileOrDescription">A boolean indicating whether to append <see cref="ITestImageProvider.SourceFileOrDescription"/> to the test output file name.</param>
    internal static void RunValidatingProcessorTest<TPixel>(
        this TestImageProvider<TPixel> provider,
        Action<IImageProcessingContext> process,
        object testOutputDetails = null,
        ImageComparer comparer = null,
        bool appendPixelTypeToFileName = true,
        bool appendSourceFileOrDescription = true)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (comparer == null)
        {
            comparer = ImageComparer.TolerantPercentage(0.001f);
        }

        using (Image<TPixel> image = provider.GetImage())
        {
            image.Mutate(process);

            image.DebugSave(
                provider,
                testOutputDetails,
                appendPixelTypeToFileName: appendPixelTypeToFileName,
                appendSourceFileOrDescription: appendSourceFileOrDescription);

            // TODO: Investigate the cause of pixel inaccuracies under Linux
            if (TestEnvironment.IsWindows)
            {
                image.CompareToReferenceOutput(
                    comparer,
                    provider,
                    testOutputDetails,
                    appendPixelTypeToFileName: appendPixelTypeToFileName,
                    appendSourceFileOrDescription: appendSourceFileOrDescription);
            }
        }
    }

    internal static void RunValidatingProcessorTest<TPixel>(
        this TestImageProvider<TPixel> provider,
        Func<IImageProcessingContext, FormattableString> processAndGetTestOutputDetails,
        ImageComparer comparer = null,
        bool appendPixelTypeToFileName = true,
        bool appendSourceFileOrDescription = true)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (comparer == null)
        {
            comparer = ImageComparer.TolerantPercentage(0.001f);
        }

        using (Image<TPixel> image = provider.GetImage())
        {
            FormattableString testOutputDetails = $"";
            image.Mutate(ctx => testOutputDetails = processAndGetTestOutputDetails(ctx));

            image.DebugSave(
                provider,
                testOutputDetails,
                appendPixelTypeToFileName: appendPixelTypeToFileName,
                appendSourceFileOrDescription: appendSourceFileOrDescription);

            // TODO: Investigate the cause of pixel inaccuracies under Linux
            if (TestEnvironment.IsWindows)
            {
                image.CompareToReferenceOutput(
                    comparer,
                    provider,
                    testOutputDetails,
                    appendPixelTypeToFileName: appendPixelTypeToFileName,
                    appendSourceFileOrDescription: appendSourceFileOrDescription);
            }
        }
    }

    public static void RunValidatingProcessorTestOnWrappedMemoryImage<TPixel>(
        this TestImageProvider<TPixel> provider,
        Action<IImageProcessingContext> process,
        object testOutputDetails = null,
        ImageComparer comparer = null,
        string useReferenceOutputFrom = null,
        bool appendPixelTypeToFileName = true,
        bool appendSourceFileOrDescription = true)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (comparer == null)
        {
            comparer = ImageComparer.TolerantPercentage(0.001f);
        }

        using (Image<TPixel> image0 = provider.GetImage())
        {
            Assert.True(image0.DangerousTryGetSinglePixelMemory(out Memory<TPixel> imageMem));
            TestMemoryManager<TPixel> mmg = TestMemoryManager<TPixel>.CreateAsCopyOf(imageMem.Span);

            using (Image<TPixel> image1 = Image.WrapMemory(mmg.Memory, image0.Width, image0.Height))
            {
                image1.Mutate(process);
                image1.DebugSave(
                    provider,
                    testOutputDetails,
                    appendPixelTypeToFileName: appendPixelTypeToFileName,
                    appendSourceFileOrDescription: appendSourceFileOrDescription);

                // TODO: Investigate the cause of pixel inaccuracies under Linux
                if (TestEnvironment.IsWindows)
                {
                    string testNameBackup = provider.Utility.TestName;

                    if (useReferenceOutputFrom != null)
                    {
                        provider.Utility.TestName = useReferenceOutputFrom;
                    }

                    image1.CompareToReferenceOutput(
                        comparer,
                        provider,
                        testOutputDetails,
                        appendPixelTypeToFileName: appendPixelTypeToFileName,
                        appendSourceFileOrDescription: appendSourceFileOrDescription);

                    provider.Utility.TestName = testNameBackup;
                }
            }
        }
    }

    /// <summary>
    /// Same as 'RunValidatingProcessorTest{TPixel}' but with an additional <see cref="Rectangle"/> parameter passed to 'process'
    /// </summary>
    internal static void RunRectangleConstrainedValidatingProcessorTest<TPixel>(
        this TestImageProvider<TPixel> provider,
        Action<IImageProcessingContext, Rectangle> process,
        object testOutputDetails = null,
        ImageComparer comparer = null,
        bool appendPixelTypeToFileName = true)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (comparer == null)
        {
            comparer = ImageComparer.TolerantPercentage(0.001f);
        }

        using (Image<TPixel> image = provider.GetImage())
        {
            Rectangle bounds = new(image.Width / 4, image.Width / 4, image.Width / 2, image.Height / 2);
            image.Mutate(x => process(x, bounds));
            image.DebugSave(provider, testOutputDetails, appendPixelTypeToFileName: appendPixelTypeToFileName);
            image.CompareToReferenceOutput(comparer, provider, testOutputDetails: testOutputDetails, appendPixelTypeToFileName: appendPixelTypeToFileName);
        }
    }

    /// <summary>
    /// Same as 'RunValidatingProcessorTest{TPixel}' but without the 'CompareToReferenceOutput()' step.
    /// </summary>
    internal static void RunProcessorTest<TPixel>(
        this TestImageProvider<TPixel> provider,
        Action<IImageProcessingContext> process,
        object testOutputDetails = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using (Image<TPixel> image = provider.GetImage())
        {
            image.Mutate(process);
            image.DebugSave(provider, testOutputDetails);
        }
    }

    public static string AsInvariantString(this FormattableString formattable) => FormattableString.Invariant(formattable);

    public static IResampler GetResampler(string name)
    {
        PropertyInfo property = typeof(KnownResamplers).GetTypeInfo().GetProperty(name);

        if (property is null)
        {
            throw new InvalidOperationException($"No resampler named '{name}");
        }

        return (IResampler)property.GetValue(null);
    }

    public static IDither GetDither(string name)
    {
        PropertyInfo property = typeof(KnownDitherings).GetTypeInfo().GetProperty(name);

        if (property is null)
        {
            throw new Exception($"No dither named '{name}");
        }

        return (IDither)property.GetValue(null);
    }

    public static string[] GetAllResamplerNames(bool includeNearestNeighbour = true)
    {
        return typeof(KnownResamplers).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(p => p.Name)
            .Where(name => includeNearestNeighbour || name != nameof(KnownResamplers.NearestNeighbor))
            .ToArray();
    }
}
