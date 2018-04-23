// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
using SixLabors.ImageSharp.PixelFormats;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
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
        public void DecodeJpegImageSharpOrig()
        {
            this.ForEachStream(ms => Image.Load<Rgba32>(ms, new OrigJpegDecoder()));
        }

        [Benchmark(Description = "DecodeJpegMultiple - ImageSharp PDFJs")]
        public void DecodeJpegImageSharpPdfJs()
        {
            this.ForEachStream(ms => Image.Load<Rgba32>(ms, new PdfJsJpegDecoder()));
        }

        [Benchmark(Baseline = true, Description = "DecodeJpegMultiple - System.Drawing")]
        public void DecodeJpegSystemDrawing()
        {
            this.ForEachStream(SDImage.FromStream);
        }
    }
}