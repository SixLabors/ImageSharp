// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using System.Drawing;
using System.IO;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortClr))]
    public class DecodeJpegParseStreamOnly
    {
        [Params(TestImages.Jpeg.Baseline.Jpeg420Exif)]
        public string TestImage { get; set; }

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        private byte[] jpegBytes;

        [GlobalSetup]
        public void Setup()
        {
            this.jpegBytes = File.ReadAllBytes(this.TestImageFullPath);
        }

        [Benchmark(Baseline = true, Description = "System.Drawing FULL")]
        public Size JpegSystemDrawing()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (var image = System.Drawing.Image.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "PdfJsJpegDecoderCore.ParseStream")]
        public void ParseStreamPdfJs()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                var decoder = new PdfJsJpegDecoderCore(Configuration.Default, new JpegDecoder() { IgnoreMetadata = true });
                decoder.ParseStream(memoryStream);
                decoder.Dispose();
            }
        }
    }
}