// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

//// #define BIG_TESTS

using System.IO;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Experimental.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class DecodeTiff
    {
        private string prevImage = null;

        private byte[] data;

        private Configuration configuration;

#if BIG_TESTS
        private static readonly int BufferSize = 1024 * 68;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, Path.Combine(TestImages.Tiff.Benchmark_Path, this.TestImage));

        [Params(
            TestImages.Tiff.Benchmark_BwFax3,
            //// TestImages.Tiff.Benchmark_RgbFax4,
            TestImages.Tiff.Benchmark_BwRle,
            TestImages.Tiff.Benchmark_GrayscaleUncompressed,
            TestImages.Tiff.Benchmark_PaletteUncompressed,
            TestImages.Tiff.Benchmark_RgbDeflate,
            TestImages.Tiff.Benchmark_RgbLzw,
            TestImages.Tiff.Benchmark_RgbPackbits,
            TestImages.Tiff.Benchmark_RgbUncompressed)]
        public string TestImage { get; set; }

#else
        private static readonly int BufferSize = Configuration.Default.StreamProcessingBufferSize;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(
            TestImages.Tiff.CcittFax3AllTermCodes,
            TestImages.Tiff.HuffmanRleAllMakeupCodes,
            TestImages.Tiff.GrayscaleUncompressed,
            TestImages.Tiff.PaletteUncompressed,
            TestImages.Tiff.RgbDeflate,
            TestImages.Tiff.RgbLzwPredictor,
            TestImages.Tiff.RgbPackbits,
            TestImages.Tiff.RgbUncompressed)]
        public string TestImage { get; set; }
#endif

        [GlobalSetup]
        public void Config()
        {
            if (this.configuration == null)
            {
                this.configuration = new Configuration();
                this.configuration.AddTiff();
                this.configuration.StreamProcessingBufferSize = BufferSize;
            }
        }

        [IterationSetup]
        public void ReadImages()
        {
            if (this.prevImage != this.TestImage)
            {
                this.data = File.ReadAllBytes(this.TestImageFullPath);
                this.prevImage = this.TestImage;
            }
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Tiff")]
        public SDSize TiffSystemDrawing()
        {
            using (var memoryStream = new MemoryStream(this.data))
            using (var image = SDImage.FromStream(memoryStream))
            {
                return image.Size;
            }
        }

        [Benchmark(Description = "ImageSharp Tiff")]
        public Size TiffCore()
        {
            using (var ms = new MemoryStream(this.data))
            using (var image = Image.Load<Rgba32>(this.configuration, ms))
            {
                return image.Size();
            }
        }
    }
}
