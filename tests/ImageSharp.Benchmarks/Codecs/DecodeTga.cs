// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.IO;
using System.Threading;
using BenchmarkDotNet.Attributes;
using ImageMagick;
using Pfim;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class DecodeTga
    {
        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        private readonly PfimConfig pfimConfig = new PfimConfig(allocator: new PfimAllocator());

        private byte[] data;

        [Params(TestImages.Tga.Bit24BottomLeft)]
        public string TestImage { get; set; }

        [GlobalSetup]
        public void SetupData()
            => this.data = File.ReadAllBytes(this.TestImageFullPath);

        [Benchmark(Baseline = true, Description = "ImageMagick Tga")]
        public int TgaImageMagick()
        {
            var settings = new MagickReadSettings { Format = MagickFormat.Tga };
            using var image = new MagickImage(new MemoryStream(this.data), settings);
            return image.Width;
        }

        [Benchmark(Description = "ImageSharp Tga")]
        public int TgaImageSharp()
        {
            using var image = Image.Load<Bgr24>(this.data, new TgaDecoder());
            return image.Width;
        }

        [Benchmark(Description = "Pfim Tga")]
        public int TgaPfim()
        {
            using var image = Targa.Create(this.data, this.pfimConfig);
            return image.Width;
        }

        private class PfimAllocator : IImageAllocator
        {
            private int rented;
            private readonly ArrayPool<byte> shared = ArrayPool<byte>.Shared;

            public byte[] Rent(int size) => this.shared.Rent(size);

            public void Return(byte[] data)
            {
                Interlocked.Decrement(ref this.rented);
                this.shared.Return(data);
            }

            public int Rented => this.rented;
        }

        /* RESULTS (07/01/2020)
        |            Method |       Runtime |           TestImage |         Mean |        Error |     StdDev | Ratio |  Gen 0 | Gen 1 | Gen 2 | Allocated |
        |------------------ |-------------- |-------------------- |-------------:|-------------:|-----------:|------:|-------:|------:|------:|----------:|
        | 'ImageMagick Tga' |    .NET 4.7.2 | Tga/targa_24bit.tga | 1,778.965 us | 1,711.088 us | 93.7905 us | 1.000 | 1.9531 |     - |     - |   13668 B |
        |  'ImageSharp Tga' |    .NET 4.7.2 | Tga/targa_24bit.tga |    38.659 us |     6.886 us |  0.3774 us | 0.022 | 0.3052 |     - |     - |    1316 B |
        |        'Pfim Tga' |    .NET 4.7.2 | Tga/targa_24bit.tga |     6.752 us |    10.268 us |  0.5628 us | 0.004 | 0.0687 |     - |     - |     313 B |
        |                   |               |                     |              |              |            |       |        |       |       |           |
        | 'ImageMagick Tga' | .NET Core 2.1 | Tga/targa_24bit.tga | 1,407.585 us |   124.215 us |  6.8087 us | 1.000 | 1.9531 |     - |     - |   13307 B |
        |  'ImageSharp Tga' | .NET Core 2.1 | Tga/targa_24bit.tga |    17.958 us |     9.352 us |  0.5126 us | 0.013 | 0.2747 |     - |     - |    1256 B |
        |        'Pfim Tga' | .NET Core 2.1 | Tga/targa_24bit.tga |     5.645 us |     2.279 us |  0.1249 us | 0.004 | 0.0610 |     - |     - |     280 B |
        */
    }
}
