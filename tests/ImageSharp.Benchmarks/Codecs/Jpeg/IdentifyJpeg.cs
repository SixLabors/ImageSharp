// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortClr))]
    public class IdentifyJpeg
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
        public IImageInfo Identify()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                var decoder = new JpegDecoder();
                return decoder.Identify(Configuration.Default, memoryStream);
            }
        }
    }
}
