// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Text;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

using SixLabors.Primitives;
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

        // [Fact]
        public void PrintWeightsData()
        {
            var size = new Size(500, 500);
            var proc = new ResizeProcessor<Rgba32>(KnownResamplers.Bicubic, 200, 200, size);

            WeightsBuffer weights = proc.PrecomputeWeights(Configuration.Default.MemoryAllocator, proc.Width, size.Width);

            var bld = new StringBuilder();

            foreach (WeightsWindow window in weights.Weights)
            {
                Span<float> span = window.GetWindowSpan();
                for (int i = 0; i < window.Length; i++)
                {
                    float value = span[i];
                    bld.Append(value);
                    bld.Append("| ");
                }

                bld.AppendLine();
            }

            File.WriteAllText("BicubicWeights.MD", bld.ToString());

            // this.Output.WriteLine(bld.ToString());
        }
    }
}