using System;
using System.IO;
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
        
        [Theory]
        [InlineData(500, 200, nameof(KnownResamplers.Bicubic))]
        [InlineData(50, 40, nameof(KnownResamplers.Bicubic))]
        [InlineData(40, 30, nameof(KnownResamplers.Bicubic))]
        [InlineData(15, 10, nameof(KnownResamplers.Bicubic))]
        [InlineData(500, 200, nameof(KnownResamplers.Lanczos8))]
        [InlineData(100, 80, nameof(KnownResamplers.Lanczos8))]
        [InlineData(100, 10, nameof(KnownResamplers.Lanczos8))]
        [InlineData(10, 100, nameof(KnownResamplers.Lanczos8))]
        public void KernelMapContentIsCorrect(int srcSize, int destSize, string resamplerName)
        {
            var resampler = (IResampler)typeof(KnownResamplers).GetProperty(resamplerName).GetValue(null);
            
            var kernelMap = KernelMap.Calculate(resampler, destSize, srcSize, Configuration.Default.MemoryAllocator);

            var referenceMap = ReferenceKernelMap.Calculate(resampler, destSize, srcSize);

#if DEBUG
            string text = PrintKernelMap(kernelMap);
            this.Output.WriteLine(text);
#endif

            for (int i = 0; i < kernelMap.DestinationSize; i++)
            {
                ResizeKernel kernel = kernelMap.GetKernel(i);

                ReferenceKernel referenceKernel = referenceMap.GetKernel(i);

                Assert.Equal(referenceKernel.Length, kernel.Length);
                Assert.Equal(referenceKernel.Left, kernel.Left);
                Assert.True(kernel.GetValues().SequenceEqual(referenceKernel.Values));
            }
        }

        private static string PrintKernelMap(KernelMap kernelMap)
        {
            var bld = new StringBuilder();

            for (int i = 0; i < kernelMap.DestinationSize; i++)
            {
                ResizeKernel kernel = kernelMap.GetKernel(i);
                bld.Append($"({kernel.Left:D3}) || ");
                Span<float> span = kernel.GetValues();

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