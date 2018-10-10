// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public class ResizeProfilingBenchmarks : MeasureFixture
    {
        public const string SkipText =
#if false
            null;
#else
            "Benchmark, enable manually!";
#endif

        private readonly Configuration configuration = Configuration.CreateDefaultInstance();

        public ResizeProfilingBenchmarks(ITestOutputHelper output)
            : base(output)
        {
            this.configuration.MaxDegreeOfParallelism = 1;
        }

        public int ExecutionCount { get; set; } = 50;
        
        [Theory(Skip = SkipText)]
        [InlineData(100, 100)]
        [InlineData(2000, 2000)]
        public void ResizeBicubic(int width, int height)
        {
            this.Measure(this.ExecutionCount,
                () =>
                    {
                        using (var image = new Image<Rgba32>(this.configuration, width, height))
                        {
                            image.Mutate(x => x.Resize(width / 5, height / 5));
                        }
                    });
        }

    }
}