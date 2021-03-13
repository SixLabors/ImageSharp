// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SharpCrc32 = ICSharpCode.SharpZipLib.Checksum.Crc32;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class Crc32Benchmark
    {
        private byte[] data;
        private readonly SharpCrc32 crc = new SharpCrc32();

        [Params(1024, 2048, 4096)]
        public int Count { get; set; }

        [GlobalSetup]
        public void SetUp()
        {
            this.data = new byte[this.Count];
            new Random(1).NextBytes(this.data);
        }

        [Benchmark(Baseline = true)]
        public long SharpZipLibCalculate()
        {
            this.crc.Reset();
            this.crc.Update(this.data);
            return this.crc.Value;
        }

        [Benchmark]
        public long SixLaborsCalculate()
        {
            return Crc32.Calculate(this.data);
        }
    }

    // ########## 17/05/2020 ##########
    //
    // |               Method |       Runtime | Count |         Mean |        Error |     StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |--------------------- |-------------- |------ |-------------:|-------------:|-----------:|------:|--------:|------:|------:|------:|----------:|
    // | SharpZipLibCalculate |    .NET 4.7.2 |  1024 |  2,797.77 ns |   278.697 ns |  15.276 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  1024 |  2,275.56 ns |   216.100 ns |  11.845 ns |  0.81 |    0.01 |     - |     - |     - |         - |
    // |                      |               |       |              |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  1024 |  2,923.43 ns | 2,656.882 ns | 145.633 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  1024 |  2,257.79 ns |    75.081 ns |   4.115 ns |  0.77 |    0.04 |     - |     - |     - |         - |
    // |                      |               |       |              |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  1024 |  2,764.14 ns |    86.281 ns |   4.729 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  1024 |     49.32 ns |     1.813 ns |   0.099 ns |  0.02 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |              |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate |    .NET 4.7.2 |  2048 |  5,603.71 ns |   427.240 ns |  23.418 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  2048 |  4,525.02 ns |    33.931 ns |   1.860 ns |  0.81 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |              |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  2048 |  5,563.32 ns |    49.337 ns |   2.704 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  2048 |  4,519.61 ns |    29.837 ns |   1.635 ns |  0.81 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |              |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  2048 |  5,543.37 ns |   518.551 ns |  28.424 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  2048 |     89.07 ns |     3.312 ns |   0.182 ns |  0.02 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |              |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate |    .NET 4.7.2 |  4096 | 11,396.95 ns |   373.450 ns |  20.470 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  4096 |  9,070.35 ns |   271.083 ns |  14.859 ns |  0.80 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |              |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  4096 | 11,127.81 ns |   239.177 ns |  13.110 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  4096 |  9,050.46 ns |   230.916 ns |  12.657 ns |  0.81 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |              |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  4096 | 11,098.62 ns |   687.978 ns |  37.710 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  4096 |    168.11 ns |     3.633 ns |   0.199 ns |  0.02 |    0.00 |     - |     - |     - |         - |
}
