// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.LongClr))]
    public class DecodeTiffBig : BenchmarkBase
    {
        private byte[] grayscale_uncompressed;

        private byte[] palette_uncompressed;

        private byte[] rgb_deflate;

        ////private byte[] rgb_jpeg;

        private byte[] rgb_lzw;

        private byte[] rgb_packbits;

        private byte[] rgb_uncompressed;

        [Params(TestImages.Tiff.GrayscaleUncompressed, TestImages.Tiff.PaletteUncompressed, TestImages.Tiff.RgbDeflate, TestImages.Tiff.RgbLzw, TestImages.Tiff.RgbPackbits, TestImages.Tiff.RgbUncompressed)]
        public string[] Images { get; set; }

        private static string FullPath => TestEnvironment.InputImagesDirectoryFullPath + "\\Benchmarks\\";

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.grayscale_uncompressed == null)
            {
                this.grayscale_uncompressed = File.ReadAllBytes(FullPath + this.Images[0]);
                this.palette_uncompressed = File.ReadAllBytes(FullPath + this.Images[1]);
                this.rgb_deflate = File.ReadAllBytes(FullPath + this.Images[2]);
                this.rgb_lzw = File.ReadAllBytes(FullPath + this.Images[3]);
                this.rgb_packbits = File.ReadAllBytes(FullPath + this.Images[4]);
                this.rgb_uncompressed = File.ReadAllBytes(FullPath + this.Images[5]);
            }
        }

        [Benchmark(Description = "Tiff grayscale_uncompressed")]
        public Size GrayscaleUncompressed()
        {
            using (var ms = new MemoryStream(this.grayscale_uncompressed))
            using (var image = Image.Load<Rgba32>(ms))
            {
                return image.Size();
            }
        }

        [Benchmark(Description = "Tiff palette_uncompressed")]
        public Size PaletteUncompressed()
        {
            using (var ms = new MemoryStream(this.palette_uncompressed))
            using (var image = Image.Load<Rgba32>(ms))
            {
                return image.Size();
            }
        }

        [Benchmark(Description = "Tiff rgb_deflate")]
        public Size RgbDeflate()
        {
            using (var ms = new MemoryStream(this.rgb_deflate))
            using (var image = Image.Load<Rgba32>(ms))
            {
                return image.Size();
            }
        }

        [Benchmark(Description = "Tiff rgb_lzw")]
        public Size RgbLzw()
        {
            using (var ms = new MemoryStream(this.rgb_lzw))
            using (var image = Image.Load<Rgba32>(ms))
            {
                return image.Size();
            }
        }

        [Benchmark(Description = "Tiff rgb_packbits")]
        public Size RgbPackbits()
        {
            using (var ms = new MemoryStream(this.rgb_packbits))
            using (var image = Image.Load<Rgba32>(ms))
            {
                return image.Size();
            }
        }

        [Benchmark(Description = "Tiff rgb_uncompressed")]
        public Size RgbUncompressed()
        {
            using (var ms = new MemoryStream(this.rgb_uncompressed))
            using (var image = Image.Load<Rgba32>(ms))
            {
                return image.Size();
            }
        }
    }
}
