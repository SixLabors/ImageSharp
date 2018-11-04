// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    /// <summary>
    /// An expensive Jpeg benchmark, running on a wide range of input images, showing aggregate results.
    /// </summary>
    [Config(typeof(MultiImageBenchmarkBase.Config))]
    public class DecodeJpegMultiple : MultiImageBenchmarkBase
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => TestImages.Jpeg.BenchmarkSuite;

        [Params(InputImageCategory.AllImages)]
        public override InputImageCategory InputCategory { get; set; }

        [Benchmark(Description = "DecodeJpegMultiple - ImageSharp")]
        public void DecodeJpegImageSharp()
        {
            this.ForEachStream(ms => Image.Load<Rgba32>(ms, new JpegDecoder()));
        }

        [Benchmark(Baseline = true, Description = "DecodeJpegMultiple - System.Drawing")]
        public void DecodeJpegSystemDrawing()
        {
            this.ForEachStream(SDImage.FromStream);
        }
    }
}