// <copyright file="DecodeJpegMultiple.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.Image
{
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;

    using Image = ImageSharp.Image;
    using ImageSharpSize = ImageSharp.Size;

    [Config(typeof(Config.Short))]
    public class DecodeJpegMultiple : MultiImageBenchmarkBase
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[]
        {
            "Jpg/"
        };

        protected override IEnumerable<string> SearchPatterns => new[] { "*.jpg" };

        [Benchmark(Description = "DecodeJpegMultiple - ImageSharp")]
        public void DecodeJpegImageSharp()
        {
            this.ForEachStream(
                ms => new ImageSharp.Image(ms)
                );  
        }

        [Benchmark(Baseline = true, Description = "DecodeJpegMultiple - System.Drawing")]
        public void DecodeJpegSystemDrawing()
        {
            this.ForEachStream(
                System.Drawing.Image.FromStream
                );
        }

    }
}