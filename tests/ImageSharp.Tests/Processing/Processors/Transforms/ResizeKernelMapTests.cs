// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;

[Trait("Category", "Processors")]
public partial class ResizeKernelMapTests
{
    private ITestOutputHelper Output { get; }

    public ResizeKernelMapTests(ITestOutputHelper output) => this.Output = output;

    /// <summary>
    /// resamplerName, srcSize, destSize
    /// </summary>
    public static readonly TheoryData<IResampler, int, int> KernelMapData
        = new()
        {
            { KnownResamplers.Bicubic, 15, 10 },
            { KnownResamplers.Bicubic, 10, 15 },
            { KnownResamplers.Bicubic, 20, 20 },
            { KnownResamplers.Bicubic, 50, 40 },
            { KnownResamplers.Bicubic, 40, 50 },
            { KnownResamplers.Bicubic, 500, 200 },
            { KnownResamplers.Bicubic, 200, 500 },
            { KnownResamplers.Bicubic, 3032, 400 },
            { KnownResamplers.Bicubic, 10, 25 },
            { KnownResamplers.Lanczos3, 16, 12 },
            { KnownResamplers.Lanczos3, 12, 16 },
            { KnownResamplers.Lanczos3, 12, 9 },
            { KnownResamplers.Lanczos3, 9, 12 },
            { KnownResamplers.Lanczos3, 6, 8 },
            { KnownResamplers.Lanczos3, 8, 6 },
            { KnownResamplers.Lanczos3, 20, 12 },
            { KnownResamplers.Lanczos3, 5, 25 },
            { KnownResamplers.Lanczos3, 5, 50 },
            { KnownResamplers.Lanczos3, 25, 5 },
            { KnownResamplers.Lanczos3, 50, 5 },
            { KnownResamplers.Lanczos3, 49, 5 },
            { KnownResamplers.Lanczos3, 31, 5 },
            { KnownResamplers.Lanczos8, 500, 200 },
            { KnownResamplers.Lanczos8, 100, 10 },
            { KnownResamplers.Lanczos8, 100, 80 },
            { KnownResamplers.Lanczos8, 10, 100 },

            // Resize_WorksWithAllResamplers_Rgba32_CalliphoraPartial_Box-0.5:
            { KnownResamplers.Box, 378, 149 },
            { KnownResamplers.Box, 349, 174 },

            // Accuracy-related regression-test cases cherry-picked from GeneratedImageResizeData
            { KnownResamplers.Box, 201, 100 },
            { KnownResamplers.Box, 199, 99 },
            { KnownResamplers.Box, 10, 299 },
            { KnownResamplers.Box, 299, 10 },
            { KnownResamplers.Box, 301, 300 },
            { KnownResamplers.Box, 1180, 480 },
            { KnownResamplers.Lanczos2, 3264, 3032 },
            { KnownResamplers.Bicubic, 1280, 2240 },
            { KnownResamplers.Bicubic, 1920, 1680 },
            { KnownResamplers.Bicubic, 3072, 2240 },
            { KnownResamplers.Welch, 300, 2008 },

            // ResizeKernel.Length -related regression tests cherry-picked from GeneratedImageResizeData
            { KnownResamplers.Bicubic, 10, 50 },
            { KnownResamplers.Bicubic, 49, 301 },
            { KnownResamplers.Bicubic, 301, 49 },
            { KnownResamplers.Bicubic, 1680, 1200 },
            { KnownResamplers.Box, 13, 299 },
            { KnownResamplers.Lanczos5, 3032, 600 },

            // Large number. https://github.com/SixLabors/ImageSharp/issues/1616
            { KnownResamplers.Bicubic, 207773, 51943 }
        };

    public static TheoryData<string, int, int> GeneratedImageResizeData = GenerateImageResizeData();

    [Theory(Skip = "Only for debugging and development")]
    [MemberData(nameof(KernelMapData))]
    public void PrintNonNormalizedKernelMap<TResampler>(TResampler resampler, int srcSize, int destSize)
        where TResampler : struct, IResampler
    {
        ReferenceKernelMap kernelMap = ReferenceKernelMap.Calculate(in resampler, destSize, srcSize, false);

        this.Output.WriteLine($"Actual KernelMap:\n{PrintKernelMap(kernelMap)}\n");
    }

    [Theory]
    [MemberData(nameof(KernelMapData))]
    public void KernelMapContentIsCorrect<TResampler>(TResampler resampler, int srcSize, int destSize)
        where TResampler : struct, IResampler
        => this.VerifyKernelMapContentIsCorrect(resampler, srcSize, destSize, false);

    // The horizontal pass uses maps with aligned kernel starts. Such a map must give the same weight at each source
    // position as the reference, with each kernel start at a multiple of 4.
    [Theory]
    [MemberData(nameof(KernelMapData))]
    public void AlignedKernelMapContentIsCorrect<TResampler>(TResampler resampler, int srcSize, int destSize)
        where TResampler : struct, IResampler
        => this.VerifyKernelMapContentIsCorrect(resampler, srcSize, destSize, true);

    // Comprehensive but expensive tests, for ResizeKernelMap.
    // Enabling them can kill you, but sometimes you have to wear the burden!
    // AppVeyor will never follow you to these shadows of Mordor.
#if false
    [Theory]
    [MemberData(nameof(GeneratedImageResizeData))]
    public void KernelMapContentIsCorrect_ExtendedGeneratedValues(string resamplerName, int srcSize, int destSize)
    {
        this.VerifyKernelMapContentIsCorrect(resamplerName, srcSize, destSize, false);
    }
#endif

    private void VerifyKernelMapContentIsCorrect<TResampler>(TResampler resampler, int srcSize, int destSize, bool alignKernelStarts)
        where TResampler : struct, IResampler
    {
        ReferenceKernelMap referenceMap = ReferenceKernelMap.Calculate(in resampler, destSize, srcSize);
        using ResizeKernelMap kernelMap = ResizeKernelMap.Calculate(in resampler, destSize, srcSize, Configuration.Default.MemoryAllocator, alignKernelStarts);

#if DEBUG
        this.Output.WriteLine(kernelMap.Info);
        this.Output.WriteLine($"Expected KernelMap:\n{PrintKernelMap(referenceMap)}\n");
        this.Output.WriteLine($"Actual KernelMap:\n{PrintKernelMap(kernelMap)}\n");
#endif

        ApproximateFloatComparer comparer = new(1e-6f);
        for (int i = 0; i < kernelMap.DestinationLength; i++)
        {
            ResizeKernel kernel = kernelMap.GetKernel((uint)i);

            ReferenceKernel referenceKernel = referenceMap.GetKernel(i);

            // Without aligned starts, every kernel of a map has the same length, and the vertical convolution reads all
            // of it. With aligned starts, a kernel starts at a multiple of 4 and covers 0 to 3 more samples in front.
            // In both cases the kernel must stay in the source, and the positions outside the reference kernel must
            // have the weight zero.
            if (alignKernelStarts)
            {
                Assert.True(
                    kernel.StartIndex % 4 == 0,
                    $"kernel.StartIndex is not a multiple of 4: {kernel.StartIndex} @ Row:{i}");
                Assert.True(
                    kernel.Length >= kernelMap.MaxDiameter && kernel.Length <= kernelMap.MaxDiameter + 3,
                    $"kernel.Length is outside [MaxDiameter, MaxDiameter + 3]: {kernel.Length}, {kernelMap.MaxDiameter} @ Row:{i}");
            }
            else
            {
                Assert.True(
                    kernel.Length == kernelMap.MaxDiameter,
                    $"kernel.Length != kernelMap.MaxDiameter: {kernel.Length} != {kernelMap.MaxDiameter} @ Row:{i}");
            }

            Assert.True(
                kernel.StartIndex >= 0 && kernel.StartIndex + kernel.Length <= srcSize,
                $"The kernel [{kernel.StartIndex}, {kernel.StartIndex + kernel.Length}) is outside the source of size {srcSize} @ Row:{i}");

            // The horizontal operators convolve PaddedLength taps, in steps of 4. The taps after the kernel length must
            // have the weight zero, so that the pixels after the kernel do not change the result.
            Assert.True(
                kernelMap.PaddedLength % 4 == 0 && kernelMap.PaddedLength >= kernel.Length,
                $"PaddedLength is not a multiple of 4 that holds the kernel: {kernelMap.PaddedLength}, {kernel.Length} @ Row:{i}");
            ref float paddedWeights = ref Unsafe.Add(ref kernelMap.GetWeightsReference(), kernel.WeightsOffset);
            for (int j = kernel.Length * 4; j < kernelMap.PaddedLength * 4; j++)
            {
                Assert.True(
                    Unsafe.Add(ref paddedWeights, j) == 0,
                    $"The padded weight {j / 4} is not zero @ Row:{i}");
            }

            // The map stores each weight 4 times, once for each channel of a pixel.
            float[] actualValues = ToWeights(kernelMap, kernel);

            // Compare the weight of each source position that one of the two kernels covers.
            int first = Math.Min(referenceKernel.Left, kernel.StartIndex);
            int last = Math.Max(referenceKernel.Left + referenceKernel.Length, kernel.StartIndex + kernel.Length);
            for (int x = first; x < last; x++)
            {
                float expected = GetWeight(referenceKernel.Values, x - referenceKernel.Left);
                float actual = GetWeight(actualValues, x - kernel.StartIndex);
                Assert.True(
                    comparer.Equals(expected, actual),
                    $"{expected} != {actual} @ (Row:{i}, Source:{x})");
            }
        }
    }

    private static float[] ToWeights(ResizeKernelMap kernelMap, in ResizeKernel kernel)
    {
        Span<float> expanded = kernelMap.GetValues(kernel);
        float[] weights = new float[kernel.Length];
        for (int j = 0; j < weights.Length; j++)
        {
            weights[j] = expanded[j * 4];
        }

        return weights;
    }

    private static float GetWeight(float[] values, int index)
        => (uint)index < (uint)values.Length ? values[index] : 0F;

    private static string PrintKernelMap(ResizeKernelMap kernelMap)
        => PrintKernelMap(kernelMap, km => km.DestinationLength, (km, i) => new ReferenceKernel(km.GetKernel((uint)i).StartIndex, ToWeights(km, km.GetKernel((uint)i))));

    private static string PrintKernelMap(ReferenceKernelMap kernelMap)
        => PrintKernelMap(kernelMap, km => km.DestinationSize, (km, i) => km.GetKernel(i));

    private static string PrintKernelMap<TKernelMap>(
        TKernelMap kernelMap,
        Func<TKernelMap, int> getDestinationSize,
        Func<TKernelMap, int, ReferenceKernel> getKernel)
    {
        StringBuilder bld = new();

        if (kernelMap is ResizeKernelMap actualMap)
        {
            bld.AppendLine(actualMap.Info);
        }

        int destinationSize = getDestinationSize(kernelMap);

        for (int i = 0; i < destinationSize; i++)
        {
            ReferenceKernel kernel = getKernel(kernelMap, i);
            bld.Append($"[{i:D3}] (L{kernel.Left:D3}) || ");
            Span<float> span = kernel.Values;

            for (int j = 0; j < kernel.Length; j++)
            {
                float value = span[j];
                bld.Append($"{value,8:F5}");
                bld.Append(" | ");
            }

            bld.AppendLine();
        }

        return bld.ToString();
    }

    private static TheoryData<string, int, int> GenerateImageResizeData()
    {
        TheoryData<string, int, int> result = new();

        string[] resamplerNames = TestUtils.GetAllResamplerNames(false);

        int[] dimensionVals =
        [

            // Arbitrary, small dimensions:
            9, 10, 11, 13, 49, 50, 53, 99, 100, 199, 200, 201, 299, 300, 301,

            // Typical image sizes:
            640, 480, 800, 600, 1024, 768, 1280, 960, 1536, 1180, 1600, 1200, 2048, 1536, 2240, 1680, 2560,
            1920, 3032, 2008, 3072, 2304, 3264, 2448
        ];

        IOrderedEnumerable<(int S, int D)> source2Dest = dimensionVals
            .SelectMany(s => dimensionVals.Select(d => (s, d)))
            .OrderBy(x => x.s + x.d);

        foreach (string resampler in resamplerNames)
        {
            foreach ((int S, int D) x in source2Dest)
            {
                result.Add(resampler, x.S, x.D);
            }
        }

        return result;
    }
}
