// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Drawing.Imaging;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class EncodeGifMultiple : MultiImageBenchmarkBase.WithImagesPreloaded
    {
        [Params(InputImageCategory.AllImages)]
        public override InputImageCategory InputCategory { get; set; }

        protected override IEnumerable<string> InputImageSubfoldersOrFiles => new[] { "Gif/" };

        [Benchmark(Description = "EncodeGifMultiple - ImageSharp")]
        public void EncodeGifImageSharp()
            => this.ForEachImageSharpImage((img, ms) =>
            {
                // Try to get as close to System.Drawing's output as possible
                var options = new GifEncoder
                {
                    Quantizer = new WebSafePaletteQuantizer(new QuantizerOptions { Dither = KnownDitherings.Bayer4x4 })
                };

                img.Save(ms, options);
                return null;
            });

        [Benchmark(Baseline = true, Description = "EncodeGifMultiple - System.Drawing")]
        public void EncodeGifSystemDrawing()
            => this.ForEachSystemDrawingImage((img, ms) =>
            {
                img.Save(ms, ImageFormat.Gif);
                return null;
            });
    }
}
