// <copyright file="DecodeJpegMultiple.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks.Image.Jpeg
{
    using System.Collections.Generic;

    using BenchmarkDotNet.Attributes;

    using CoreImage = SixLabors.ImageSharp.Image;

    [Config(typeof(Config.ShortClr))]
    public class DecodeJpegMultiple : MultiImageBenchmarkBase
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[]
        {
            "Jpg/baseline",
            "Jpg/progressive",
        };

        protected override IEnumerable<string> SearchPatterns => new[] { "*.jpg" };

        [Benchmark(Description = "DecodeJpegMultiple - ImageSharp")]
        public void DecodeJpegImageSharpNwq()
        {
            this.ForEachStream(
                ms => CoreImage.Load<Rgba32>(ms)
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