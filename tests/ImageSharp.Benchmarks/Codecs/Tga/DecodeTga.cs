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

    // ImageMagick and ImageSharp read from this stream. Each call rewinds it, so the results measure the decoders only.
    // Pfim reads the byte array.
    private MemoryStream inputStream;

    [Params(TestImages.Tga.Bit24BottomLeft)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void SetupData()
    {
        this.data = File.ReadAllBytes(this.TestImageFullPath);
        this.inputStream = new MemoryStream(this.data, false);
    }

    /// <summary>
    /// Releases the shared input stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.inputStream.Dispose();
        this.inputStream = null;
    }

    [Benchmark(Baseline = true, Description = "ImageMagick Tga")]
    public uint TgaImageMagick()
    {
        MagickReadSettings settings = new() { Format = MagickFormat.Tga };
        this.inputStream.Position = 0;
        using MagickImage image = new(this.inputStream, settings);
        return image.Width;
    }

    [Benchmark(Description = "ImageSharp Tga")]
    public int TgaImageSharp()
    {
        this.inputStream.Position = 0;
        using Image<Bgr24> image = Image.Load<Bgr24>(this.inputStream);
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

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method            | TestImage           | Mean       | Error       | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
    |------------------ |-------------------- |-----------:|------------:|----------:|------:|-------:|----------:|------------:|
    | 'ImageMagick Tga' | Tga/targa_24bit.tga | 678.098 us | 118.5345 us | 6.4973 us | 1.000 |      - |    4741 B |        1.00 |
    | 'ImageSharp Tga'  | Tga/targa_24bit.tga |  14.749 us |   2.3542 us | 0.1290 us | 0.022 | 0.1221 |    2058 B |        0.43 |
    | 'Pfim Tga'        | Tga/targa_24bit.tga |   1.322 us |   0.2019 us | 0.0111 us | 0.002 | 0.0134 |     248 B |        0.05 |

    Allocated reports managed memory only.
    */
}
