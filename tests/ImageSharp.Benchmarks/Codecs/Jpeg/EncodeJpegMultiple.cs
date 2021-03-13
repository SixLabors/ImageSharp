// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class EncodeJpegMultiple : MultiImageBenchmarkBase.WithImagesPreloaded
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[] { "Bmp/", "Jpg/baseline" };

        protected override IEnumerable<string> SearchPatterns => new[] { "*.bmp", "*.jpg" };

        [Benchmark(Description = "EncodeJpegMultiple - ImageSharp")]
        public void EncodeJpegImageSharp()
            => this.ForEachImageSharpImage((img, ms) =>
            {
                img.Save(ms, new JpegEncoder());
                return null;
            });

        [Benchmark(Baseline = true, Description = "EncodeJpegMultiple - System.Drawing")]
        public void EncodeJpegSystemDrawing()
            => this.ForEachSystemDrawingImage((img, ms) =>
            {
                img.Save(ms, ImageFormat.Jpeg);
                return null;
            });
    }
}
