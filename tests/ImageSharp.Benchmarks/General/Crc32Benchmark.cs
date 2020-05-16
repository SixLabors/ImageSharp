// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SharpCrc32 = ICSharpCode.SharpZipLib.Checksum.Crc32;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    [Config(typeof(Config.ShortClr))]
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
    // |               Method |       Runtime | Count |         Mean |        Error |       StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |--------------------- |-------------- |------ |-------------:|-------------:|-------------:|------:|--------:|------:|------:|------:|----------:|
    // | SharpZipLibCalculate |    .NET 4.7.2 |  1024 |  3,067.24 ns |    769.25 ns |    42.165 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  1024 |  2,546.86 ns |  1,106.36 ns |    60.643 ns |  0.83 |    0.02 |     - |     - |     - |         - |
    // |                      |               |       |              |              |              |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  1024 |  3,377.15 ns |  3,903.41 ns |   213.959 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  1024 |  2,524.25 ns |  2,220.97 ns |   121.739 ns |  0.75 |    0.04 |     - |     - |     - |         - |
    // |                      |               |       |              |              |              |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  1024 |  3,980.60 ns |  8,497.37 ns |   465.769 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  1024 |     78.68 ns |     69.82 ns |     3.827 ns |  0.02 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |              |              |              |       |         |       |       |       |           |
    // | SharpZipLibCalculate |    .NET 4.7.2 |  2048 |  7,934.29 ns | 42,550.13 ns | 2,332.316 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  2048 |  5,437.81 ns | 12,760.51 ns |   699.447 ns |  0.71 |    0.10 |     - |     - |     - |         - |
    // |                      |               |       |              |              |              |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  2048 |  6,008.05 ns |    621.37 ns |    34.059 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  2048 |  4,791.50 ns |  3,894.94 ns |   213.495 ns |  0.80 |    0.04 |     - |     - |     - |         - |
    // |                      |               |       |              |              |              |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  2048 |  5,900.06 ns |  1,344.70 ns |    73.707 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  2048 |    103.12 ns |     15.66 ns |     0.859 ns |  0.02 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |              |              |              |       |         |       |       |       |           |
    // | SharpZipLibCalculate |    .NET 4.7.2 |  4096 | 12,422.59 ns |  1,308.01 ns |    71.696 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  4096 | 10,524.63 ns |  6,267.56 ns |   343.546 ns |  0.85 |    0.03 |     - |     - |     - |         - |
    // |                      |               |       |              |              |              |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  4096 | 11,888.00 ns |  1,059.25 ns |    58.061 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  4096 |  9,806.24 ns |    241.91 ns |    13.260 ns |  0.82 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |              |              |              |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  4096 | 12,181.28 ns |  1,974.68 ns |   108.239 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  4096 |    192.39 ns |     10.27 ns |     0.563 ns |  0.02 |    0.00 |     - |     - |     - |         - |
}
