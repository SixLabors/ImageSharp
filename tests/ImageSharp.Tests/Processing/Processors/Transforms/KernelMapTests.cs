// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public partial class KernelMapTests
    {
        private ITestOutputHelper Output { get; }

        public KernelMapTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        /// <summary>
        /// resamplerName, srcSize, destSize
        /// </summary>
        public static readonly TheoryData<string, int, int> KernelMapData = new TheoryData<string, int, int>
        {
            { nameof(KnownResamplers.Bicubic), 15, 10 },
            { nameof(KnownResamplers.Bicubic), 10, 15 },
            { nameof(KnownResamplers.Bicubic), 20, 20 },
            { nameof(KnownResamplers.Bicubic), 50, 40 },
            { nameof(KnownResamplers.Bicubic), 40, 50 },
            { nameof(KnownResamplers.Bicubic), 500, 200 },
            { nameof(KnownResamplers.Bicubic), 200, 500 },

            { nameof(KnownResamplers.Bicubic), 10, 25 },

            { nameof(KnownResamplers.Lanczos3), 16, 12 },
            { nameof(KnownResamplers.Lanczos3), 12, 16 },
            { nameof(KnownResamplers.Lanczos3), 12, 9 },
            { nameof(KnownResamplers.Lanczos3), 9, 12 },
            { nameof(KnownResamplers.Lanczos3), 6, 8 },
            { nameof(KnownResamplers.Lanczos3), 8, 6 },
            { nameof(KnownResamplers.Lanczos3), 20, 12 },

            { nameof(KnownResamplers.Lanczos3), 5, 25 },
            { nameof(KnownResamplers.Lanczos3), 5, 50 },

            { nameof(KnownResamplers.Lanczos3), 25, 5 },
            { nameof(KnownResamplers.Lanczos3), 50, 5 },
            { nameof(KnownResamplers.Lanczos3), 49, 5 },
            { nameof(KnownResamplers.Lanczos3), 31, 5 },

            { nameof(KnownResamplers.Lanczos8), 500, 200 },
            { nameof(KnownResamplers.Lanczos8), 100, 10 },
            { nameof(KnownResamplers.Lanczos8), 100, 80 },
            { nameof(KnownResamplers.Lanczos8), 10, 100 },
        };

        public static TheoryData<string, int, int> GeneratedImageResizeData =
            GenerateImageResizeData();


        [Theory]
        [MemberData(nameof(KernelMapData))]
        public void PrintNonNormalizedKernelMap(string resamplerName, int srcSize, int destSize)
        {
            IResampler resampler = TestUtils.GetResampler(resamplerName);

            var kernelMap = ReferenceKernelMap.Calculate(resampler, destSize, srcSize, false);

            this.Output.WriteLine($"Actual KernelMap:\n{PrintKernelMap(kernelMap)}\n");
        }

        [Theory]
        [MemberData(nameof(KernelMapData))]
        //[MemberData(nameof(GeneratedImageResizeData))]
        public void KernelMapContentIsCorrect(string resamplerName, int srcSize, int destSize)
        {
            VerifyKernelMapContentIsCorrect(resamplerName, srcSize, destSize);
        }

        [Theory]
        [MemberData(nameof(GeneratedImageResizeData))]
        public void KernelMapContentIsCorrect_ExtendedGeneratedValues(string resamplerName, int srcSize, int destSize)
        {
            VerifyKernelMapContentIsCorrect(resamplerName, srcSize, destSize);
        }


        private static void VerifyKernelMapContentIsCorrect(string resamplerName, int srcSize, int destSize)
        {
            IResampler resampler = TestUtils.GetResampler(resamplerName);

            var referenceMap = ReferenceKernelMap.Calculate(resampler, destSize, srcSize);
            var kernelMap = ResizeKernelMap.Calculate(resampler, destSize, srcSize, Configuration.Default.MemoryAllocator);

#if DEBUG
            // this.Output.WriteLine($"Expected KernelMap:\n{PrintKernelMap(referenceMap)}\n");
            // this.Output.WriteLine($"Actual KernelMap:\n{PrintKernelMap(kernelMap)}\n");
#endif

            for (int i = 0; i < kernelMap.DestinationLength; i++)
            {
                ResizeKernel kernel = kernelMap.GetKernel(i);

                ReferenceKernel referenceKernel = referenceMap.GetKernel(i);

                Assert.True(
                    referenceKernel.Length == kernel.Length,
                    $"referenceKernel.Length != kernel.Length: {referenceKernel.Length} != {kernel.Length}");
                Assert.True(
                    referenceKernel.Left == kernel.Left,
                    $"referenceKernel.Left != kernel.Left: {referenceKernel.Left} != {kernel.Left}");
                float[] expectedValues = referenceKernel.Values;
                Span<float> actualValues = kernel.Values;

                Assert.Equal(expectedValues.Length, actualValues.Length);

                var comparer = new ApproximateFloatComparer(1e-6f);

                for (int x = 0; x < expectedValues.Length; x++)
                {
                    Assert.True(
                        comparer.Equals(expectedValues[x], actualValues[x]),
                        $"{expectedValues[x]} != {actualValues[x]} @ (Row:{i}, Col:{x})");
                }
            }
        }

        private static string PrintKernelMap(ResizeKernelMap kernelMap) =>
            PrintKernelMap(kernelMap, km => km.DestinationLength, (km, i) => km.GetKernel(i));

        private static string PrintKernelMap(ReferenceKernelMap kernelMap) =>
            PrintKernelMap(kernelMap, km => km.DestinationSize, (km, i) => km.GetKernel(i));

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

            string[] resamplerNames = typeof(KnownResamplers).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Select(p => p.Name).ToArray();

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