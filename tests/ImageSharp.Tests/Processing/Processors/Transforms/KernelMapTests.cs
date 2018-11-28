using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.Primitives;

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

            { nameof(KnownResamplers.Lanczos8), 500, 200 },
            { nameof(KnownResamplers.Lanczos8), 100, 10 },
            { nameof(KnownResamplers.Lanczos8), 100, 80 },
            { nameof(KnownResamplers.Lanczos8), 10, 100 },
        };

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
        public void KernelMapContentIsCorrect(string resamplerName, int srcSize, int destSize)
        {
            IResampler resampler = TestUtils.GetResampler(resamplerName);

            var referenceMap = ReferenceKernelMap.Calculate(resampler, destSize, srcSize);
            var kernelMap = ResizeKernelMap.Calculate(resampler, destSize, srcSize, Configuration.Default.MemoryAllocator);

#if DEBUG
            this.Output.WriteLine($"Actual KernelMap:\n{PrintKernelMap(kernelMap)}\n");
            // this.Output.WriteLine($"Reference KernelMap:\n{PrintKernelMap(referenceMap)}\n");
#endif

            for (int i = 0; i < kernelMap.DestinationSize; i++)
            {
                ResizeKernel kernel = kernelMap.GetKernel(i);

                ReferenceKernel referenceKernel = referenceMap.GetKernel(i);

                Assert.Equal(referenceKernel.Length, kernel.Length);
                Assert.Equal(referenceKernel.Left, kernel.Left);
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
            PrintKernelMap(kernelMap, km => km.DestinationSize, (km, i) => km.GetKernel(i));

        private static string PrintKernelMap(ReferenceKernelMap kernelMap) =>
            PrintKernelMap(kernelMap, km => km.DestinationSize, (km, i) => km.GetKernel(i));

        private static string PrintKernelMap<TKernelMap>(
            TKernelMap kernelMap,
            Func<TKernelMap, int> getDestinationSize,
            Func<TKernelMap, int, ReferenceKernel> getKernel)
        {
            var bld = new StringBuilder();

            int destinationSize = getDestinationSize(kernelMap);

            for (int i = 0; i < destinationSize; i++)
            {
                ReferenceKernel kernel = getKernel(kernelMap, i);
                bld.Append($"({kernel.Left:D3}) || ");
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
    }
}