// <copyright file="EncodeJpegMultiple.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.Image
{
    using System.Collections.Generic;
    using System.Drawing.Imaging;

    using BenchmarkDotNet.Attributes;

    using ImageSharp.Formats;

    
    [Config(typeof(Config.Short))] // It's long enough to iterate through multiple files
    public class EncodeJpegMultiple : MultiImageBenchmarkBase.WithImagesPreloaded
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[] { "Bmp/", "Jpg/baseline" };

        protected override IEnumerable<string> SearchPatterns => new[] { "*.bmp", "*.jpg" };

        [Benchmark(Description = "EncodeJpegMultiple - ImageSharp")]
        public void EncodeJpegImageSharp()
        {
            this.ForEachImageSharpImage(
                (img, ms) =>
                    {
                        img.Save(ms, new JpegEncoder());
                        return null;
                    });
        }

        [Benchmark(Baseline = true, Description = "EncodeJpegMultiple - System.Drawing")]
        public void EncodeJpegSystemDrawing()
        {
            this.ForEachSystemDrawingImage(
                (img, ms) =>
                    {
                        img.Save(ms, ImageFormat.Jpeg);
                        return null;
                    });
        }
    }
}