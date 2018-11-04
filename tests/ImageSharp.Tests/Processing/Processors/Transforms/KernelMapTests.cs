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
    public class KernelMapTests
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
        public void PrintKernelMap(int srcSize, int destSize, string resamplerName)
        {
            var resampler = (IResampler)typeof(KnownResamplers).GetProperty(resamplerName).GetValue(null);
            
            var kernelMap = KernelMap.Calculate(resampler, destSize, srcSize, Configuration.Default.MemoryAllocator);

            var bld = new StringBuilder();

            foreach (ResizeKernel kernel in kernelMap.Kernels)
            {
                bld.Append($"({kernel.Left:D3}) || ");
                Span<float> span = kernel.GetValues();
                for (int i = 0; i < kernel.Length; i++)
                {
                    float value = span[i];
                    bld.Append($"{value,7:F5}");
                    bld.Append(" | ");
                }

                bld.AppendLine();
            }

            string outDir = TestEnvironment.CreateOutputDirectory("." + nameof(this.PrintKernelMap));
            string fileName = $@"{outDir}\{resamplerName}_{srcSize}_{destSize}.MD";

            File.WriteAllText(fileName, bld.ToString());

            this.Output.WriteLine(bld.ToString());
        }
    }
}