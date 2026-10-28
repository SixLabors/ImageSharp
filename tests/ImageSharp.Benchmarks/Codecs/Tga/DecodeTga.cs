// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using BenchmarkDotNet.Attributes;
using ImageMagick;
using Pfim;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class DecodeTga
{
    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    private readonly PfimConfig pfimConfig = new(allocator: new PfimAllocator());

    private byte[] data;

    [Params(TestImages.Tga.Bit24BottomLeft)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void SetupData()
        => this.data = File.ReadAllBytes(this.TestImageFullPath);

    [Benchmark(Baseline = true, Description = "ImageMagick Tga")]
    public uint TgaImageMagick()
    {
        MagickReadSettings settings = new() { Format = MagickFormat.Tga };
        using MagickImage image = new(new MemoryStream(this.data), settings);
        return image.Width;
    }

    [Benchmark(Description = "ImageSharp Tga")]
    public int TgaImageSharp()
    {
        using Image<Bgr24> image = Image.Load<Bgr24>(this.data);
        return image.Width;
    }

    [Benchmark(Description = "Pfim Tga")]
    public int TgaPfim()
    {
        using Targa image = Targa.Create(this.data, this.pfimConfig);
        return image.Width;
    }

    private sealed class PfimAllocator : IImageAllocator
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

    /* Results 28.10.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method             | TestImage            |         Mean |        Error |      StdDev |   Ratio |     Gen0 |   Allocated |   Alloc Ratio |
    | ------------------ | -------------------- | -----------: | -----------: | ----------: | ------: | -------: | ----------: | ------------: |
    | 'ImageMagick Tga'  | Tga/targa_24bit.tga  |   631.777 μs |   25.8058 μs |   1.4145 μs |   1.000 |        - |      4805 B |          1.00 |
    | 'ImageSharp Tga'   | Tga/targa_24bit.tga  |    14.277 μs |    1.3643 μs |   0.0748 μs |   0.023 |   0.1221 |      2114 B |          0.44 |
    | 'Pfim Tga'         | Tga/targa_24bit.tga  |     1.319 μs |    0.1189 μs |   0.0065 μs |   0.002 |   0.0134 |       248 B |          0.05 |

    Allocated reports managed memory only.
    */
}
