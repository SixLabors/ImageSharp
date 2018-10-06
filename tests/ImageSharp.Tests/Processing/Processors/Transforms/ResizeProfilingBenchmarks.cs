// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
    public class ResizeProfilingBenchmarks : MeasureFixture
    {
        public ResizeProfilingBenchmarks(ITestOutputHelper output)
            : base(output)
        {
        }

        public int ExecutionCount { get; set; } = 50;

        // [Theory] // Benchmark, enable manually!
        // [InlineData(100, 100)]
        // [InlineData(2000, 2000)]
        public void ResizeBicubic(int width, int height)
        {
            this.Measure(this.ExecutionCount,
                () =>
                    {
                        using (var image = new Image<Rgba32>(width, height))
                        {
                            image.Mutate(x => x.Resize(width / 4, height / 4));
                        }
                    });
        }

        [Theory]
        [InlineData(500, 200, nameof(KnownResamplers.Bicubic))]
        [InlineData(50, 40, nameof(KnownResamplers.Bicubic))]
        [InlineData(40, 30, nameof(KnownResamplers.Bicubic))]
        [InlineData(500, 200, nameof(KnownResamplers.Lanczos8))]
        [InlineData(100, 80, nameof(KnownResamplers.Lanczos8))]
        [InlineData(100, 10, nameof(KnownResamplers.Lanczos8))]
        [InlineData(10, 100, nameof(KnownResamplers.Lanczos8))]
        public void PrintWeightsData(int srcSize, int destSize, string resamplerName)
        {
            var size = new Size(srcSize, srcSize);
            var resampler = (IResampler) typeof(KnownResamplers).GetProperty(resamplerName).GetValue(null);
            var proc = new ResizeProcessor<Rgba32>(resampler, destSize, destSize, size);

            WeightsBuffer weights = proc.PrecomputeWeights(Configuration.Default.MemoryAllocator, proc.Width, size.Width);

            var bld = new StringBuilder();

            foreach (WeightsWindow window in weights.Weights)
            {
                Span<float> span = window.GetWindowSpan();
                for (int i = 0; i < window.Length; i++)
                {
                    float value = span[i];
                    bld.Append($"{value,7:F4}");
                    bld.Append("| ");
                }

                bld.AppendLine();
            }

            string outDir = TestEnvironment.CreateOutputDirectory("." + nameof(this.PrintWeightsData));
            string fileName = $@"{outDir}\{resamplerName}_{srcSize}_{destSize}.MD";

            File.WriteAllText(fileName, bld.ToString());

            this.Output.WriteLine(bld.ToString());
        }
    }
}