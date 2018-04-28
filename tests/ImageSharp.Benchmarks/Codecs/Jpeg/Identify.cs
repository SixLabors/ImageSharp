// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortClr))]
    public class Identify
    {
        private byte[] jpegBytes;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(TestImages.Jpeg.Baseline.Jpeg420Exif, TestImages.Jpeg.Baseline.Calliphora)]
        public string TestImage { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.jpegBytes == null)
            {
                this.jpegBytes = File.ReadAllBytes(this.TestImageFullPath);
            }
        }

        [Benchmark]
        public IImageInfo IdentifyGolang()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                var decoder = new OrigJpegDecoder();

                return decoder.Identify(Configuration.Default, memoryStream);
            }
        }

        [Benchmark]
        public IImageInfo IdentifyPdfJs()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                var decoder = new PdfJsJpegDecoder();
                return decoder.Identify(Configuration.Default, memoryStream);
            }
        }
    }
}
