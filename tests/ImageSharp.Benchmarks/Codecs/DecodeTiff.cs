// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing;
using System.IO;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;

using CoreImage = SixLabors.ImageSharp.Image;
using CoreSize = SixLabors.Primitives.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    public class DecodeTiff : BenchmarkBase
    {
        private byte[] tiffBytes;

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.tiffBytes == null)
            {
                this.tiffBytes = File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Tiff/Calliphora_rgb_uncompressed.tiff");
            }
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Tiff")]
        public Size TiffSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.tiffBytes))
            {
                using (var image = System.Drawing.Image.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "ImageSharp Tiff")]
        public CoreSize TiffCore()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.tiffBytes))
            {
                using (Image<Rgba32> image = CoreImage.Load<Rgba32>(memoryStream))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }
    }
}
