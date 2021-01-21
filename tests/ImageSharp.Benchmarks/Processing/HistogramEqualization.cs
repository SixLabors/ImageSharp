// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Processing
{
    [Config(typeof(Config.MultiFramework))]
    public class HistogramEqualization
    {
        private Image<Rgba32> image;

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.image == null)
            {
                this.image = Image.Load<Rgba32>(File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Jpeg.Baseline.HistogramEqImage)));
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.image.Dispose();
            this.image = null;
        }

        [Benchmark(Description = "Global Histogram Equalization")]
        public void GlobalHistogramEqualization()
            => this.image.Mutate(img => img.HistogramEqualization(
                new HistogramEqualizationOptions()
                {
                    LuminanceLevels = 256,
                    Method = HistogramEqualizationMethod.Global
                }));

        [Benchmark(Description = "AdaptiveHistogramEqualization (Tile interpolation)")]
        public void AdaptiveHistogramEqualization()
            => this.image.Mutate(img => img.HistogramEqualization(
                new HistogramEqualizationOptions()
                {
                    LuminanceLevels = 256,
                    Method = HistogramEqualizationMethod.AdaptiveTileInterpolation
                }));
    }
}
