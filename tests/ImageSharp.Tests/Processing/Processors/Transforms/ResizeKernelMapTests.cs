// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Text;

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public partial class ResizeKernelMapTests
    {
        private ITestOutputHelper Output { get; }

        public ResizeKernelMapTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        /// <summary>
        /// resamplerName, srcSize, destSize
        /// </summary>
        public static readonly TheoryData<IResampler, int, int> KernelMapData
            = new TheoryData<IResampler, int, int>
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
        };

        public static TheoryData<string, int, int> GeneratedImageResizeData =
            GenerateImageResizeData();

        [Theory(Skip = "Only for debugging and development")]
        [MemberData(nameof(KernelMapData))]
        public void PrintNonNormalizedKernelMap<TResampler>(TResampler resampler, int srcSize, int destSize)
            where TResampler : struct, IResampler
        {
            var kernelMap = ReferenceKernelMap.Calculate<TResampler>(in resampler, destSize, srcSize, false);

            this.Output.WriteLine($"Actual KernelMap:\n{PrintKernelMap(kernelMap)}\n");
        }

        [Theory]
        [MemberData(nameof(KernelMapData))]
        public void KernelMapContentIsCorrect<TResampler>(TResampler resampler, int srcSize, int destSize)
            where TResampler : struct, IResampler
        {
            this.VerifyKernelMapContentIsCorrect(resampler, srcSize, destSize);
        }

        // Comprehensive but expensive tests, for ResizeKernelMap.
        // Enabling them can kill you, but sometimes you have to wear the burden!
        // AppVeyor will never follow you to these shadows of Mordor.
#if false
        [Theory]
        [MemberData(nameof(GeneratedImageResizeData))]
        public void KernelMapContentIsCorrect_ExtendedGeneratedValues(string resamplerName, int srcSize, int destSize)
        {
            this.VerifyKernelMapContentIsCorrect(resamplerName, srcSize, destSize);
        }
#endif

        private void VerifyKernelMapContentIsCorrect<TResampler>(TResampler resampler, int srcSize, int destSize)
            where TResampler : struct, IResampler
        {
            var referenceMap = ReferenceKernelMap.Calculate(in resampler, destSize, srcSize);
            var kernelMap = ResizeKernelMap.Calculate(in resampler, destSize, srcSize, Configuration.Default.MemoryAllocator);

#if DEBUG
            this.Output.WriteLine(kernelMap.Info);
            this.Output.WriteLine($"Expected KernelMap:\n{PrintKernelMap(referenceMap)}\n");
            this.Output.WriteLine($"Actual KernelMap:\n{PrintKernelMap(kernelMap)}\n");
#endif
            var comparer = new ApproximateFloatComparer(1e-6f);

            for (int i = 0; i < kernelMap.DestinationLength; i++)
            {
                ResizeKernel kernel = kernelMap.GetKernel(i);

                ReferenceKernel referenceKernel = referenceMap.GetKernel(i);

                Assert.True(
                    referenceKernel.Length == kernel.Length,
                    $"referenceKernel.Length != kernel.Length: {referenceKernel.Length} != {kernel.Length}");
                Assert.True(
                    referenceKernel.Left == kernel.StartIndex,
                    $"referenceKernel.Left != kernel.Left: {referenceKernel.Left} != {kernel.StartIndex}");
                float[] expectedValues = referenceKernel.Values;
                Span<float> actualValues = kernel.Values;

                Assert.Equal(expectedValues.Length, actualValues.Length);

                for (int x = 0; x < expectedValues.Length; x++)
                {
                    Assert.True(
                        comparer.Equals(expectedValues[x], actualValues[x]),
                        $"{expectedValues[x]} != {actualValues[x]} @ (Row:{i}, Col:{x})");
                }
            }
        }

        private static string PrintKernelMap(ResizeKernelMap kernelMap)
            => PrintKernelMap(kernelMap, km => km.DestinationLength, (km, i) => km.GetKernel(i));

        private static string PrintKernelMap(ReferenceKernelMap kernelMap)
            => PrintKernelMap(kernelMap, km => km.DestinationSize, (km, i) => km.GetKernel(i));

        private static string PrintKernelMap<TKernelMap>(
            TKernelMap kernelMap,
            Func<TKernelMap, int> getDestinationSize,
            Func<TKernelMap, int, ReferenceKernel> getKernel)
        {
            var bld = new StringBuilder();

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
            var result = new TheoryData<string, int, int>();

            string[] resamplerNames = TestUtils.GetAllResamplerNames(false);

            int[] dimensionVals =
                {
                    // Arbitrary, small dimensions:
                    9, 10, 11, 13, 49, 50, 53, 99, 100, 199, 200, 201, 299, 300, 301,

                    // Typical image sizes:
                    640, 480, 800, 600, 1024, 768, 1280, 960, 1536, 1180, 1600, 1200, 2048, 1536, 2240, 1680, 2560,
                    1920, 3032, 2008, 3072, 2304, 3264, 2448
                };

            IOrderedEnumerable<(int s, int d)> source2Dest = dimensionVals
                .SelectMany(s => dimensionVals.Select(d => (s, d)))
                .OrderBy(x => x.s + x.d);

            foreach (string resampler in resamplerNames)
            {
                foreach ((int s, int d) x in source2Dest)
                {
                    result.Add(resampler, x.s, x.d);
                }
            }

            return result;
        }
    }
}
