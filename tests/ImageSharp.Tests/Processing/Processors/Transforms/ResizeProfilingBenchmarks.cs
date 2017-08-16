// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Text;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
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

        // [Fact]
        public void PrintWeightsData()
        {
            var proc = new ResizeProcessor<Rgba32>(new BicubicResampler(), 200, 200);

            ResamplingWeightedProcessor<Rgba32>.WeightsBuffer weights = proc.PrecomputeWeights(200, 500);

            var bld = new StringBuilder();

            foreach (ResamplingWeightedProcessor<Rgba32>.WeightsWindow window in weights.Weights)
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