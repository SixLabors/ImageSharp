// <copyright file="EncodeBmpMultiple.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.Image
{
    using System.Collections.Generic;
    using System.Drawing.Imaging;

    using BenchmarkDotNet.Attributes;

    using ImageSharp.Formats;

    [Config(typeof(Config.Short))]
    public class EncodeBmpMultiple : MultiImageBenchmarkBase.WithImagesPreloaded
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[] { "Bmp/", "Jpg/baseline" };

        [Benchmark(Description = "EncodeBmpMultiple - ImageSharp")]
        public void EncodeBmpImageSharp()
        {
            this.ForEachImageSharpImage(
                (img, ms) =>
                    {
                        img.Save(ms, new BmpEncoder());
                        return null;
                    });
        }

        [Benchmark(Baseline = true, Description = "EncodeBmpMultiple - System.Drawing")]
        public void EncodeBmpSystemDrawing()
        {
            this.ForEachSystemDrawingImage(
                (img, ms) =>
                    {
                        img.Save(ms, ImageFormat.Bmp);
                        return null;
                    });
        }
    }
}