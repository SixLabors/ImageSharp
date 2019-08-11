//Copyright(c) Six Labors and contributors.
//Licensed under the Apache License, Version 2.0.

using System.IO;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;

using CoreImage = SixLabors.ImageSharp.Image;
using CoreSize = SixLabors.Primitives.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    public abstract class DecodeTiffBase : BenchmarkBase
    {
        private byte[] grayscale_uncompressed;

        private byte[] palette_uncompressed;

        private byte[] rgb_deflate;

        ////private byte[] rgb_jpeg;

        private byte[] rgb_lzw;

        private byte[] rgb_packbits;

        private byte[] rgb_uncompressed;

        protected abstract string FileNamePrefix { get; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.grayscale_uncompressed == null)
            {
                string path = @"C:\Work\GitHub\SixLabors.ImageSharp\tests\ImageSharp.Tests\TestImages\Formats\Tiff\";
                this.grayscale_uncompressed = File.ReadAllBytes(path + this.FileNamePrefix + "_grayscale_uncompressed.tiff");
                this.palette_uncompressed = File.ReadAllBytes(path + this.FileNamePrefix + "_palette_uncompressed.tiff");
                this.rgb_deflate = File.ReadAllBytes(path + this.FileNamePrefix + "_rgb_deflate.tiff");
                ////this.rgb_jpeg = File.ReadAllBytes(path + this.FileNamePrefix + "_rgb_jpeg.tiff");
                this.rgb_lzw = File.ReadAllBytes(path + this.FileNamePrefix + "_rgb_lzw.tiff");
                this.rgb_packbits = File.ReadAllBytes(path + this.FileNamePrefix + "_rgb_packbits.tiff");
                this.rgb_uncompressed = File.ReadAllBytes(path + this.FileNamePrefix + "_rgb_uncompressed.tiff");
            }
        }

        [Benchmark(Description = "Tiff grayscale_uncompressed")]
        public CoreSize GrayscaleUncompressed()
        {
            using (MemoryStream ms = new MemoryStream(this.grayscale_uncompressed))
            using (Image<Rgba32> image = CoreImage.Load<Rgba32>(ms))
            {
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "Tiff palette_uncompressed")]
        public CoreSize PaletteUncompressed()
        {
            using (MemoryStream ms = new MemoryStream(this.palette_uncompressed))
            using (Image<Rgba32> image = CoreImage.Load<Rgba32>(ms))
            {
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "Tiff rgb_deflate")]
        public CoreSize RgbDeflate()
        {
            using (MemoryStream ms = new MemoryStream(this.rgb_deflate))
            using (Image<Rgba32> image = CoreImage.Load<Rgba32>(ms))
            {
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "Tiff rgb_lzw")]
        public CoreSize RgbLzw()
        {
            using (MemoryStream ms = new MemoryStream(this.rgb_lzw))
            using (Image<Rgba32> image = CoreImage.Load<Rgba32>(ms))
            {
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "Tiff rgb_packbits")]
        public CoreSize RgbPackbits()
        {
            using (MemoryStream ms = new MemoryStream(this.rgb_packbits))
            using (Image<Rgba32> image = CoreImage.Load<Rgba32>(ms))
            {
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "Tiff rgb_uncompressed")]
        public CoreSize RgbUncompressed()
        {
            using (MemoryStream ms = new MemoryStream(this.rgb_uncompressed))
            using (Image<Rgba32> image = CoreImage.Load<Rgba32>(ms))
            {
                return new CoreSize(image.Width, image.Height);
            }
        }
    }
}
