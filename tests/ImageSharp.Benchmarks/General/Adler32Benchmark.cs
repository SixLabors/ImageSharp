// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SharpAdler32 = ICSharpCode.SharpZipLib.Checksum.Adler32;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    [Config(typeof(Config.ShortClr))]
    public class Adler32Benchmark
    {
        private byte[] data;
        private readonly SharpAdler32 adler = new SharpAdler32();

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
            this.adler.Reset();
            this.adler.Update(this.data);
            return this.adler.Value;
        }

        [Benchmark]
        public uint SixLaborsCalculate()
        {
            return Adler32.Calculate(this.data);
        }
    }

    // ########## 17/05/2020 ##########
    //
    // |               Method |       Runtime | Count |        Mean |        Error |     StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |--------------------- |-------------- |------ |------------:|-------------:|-----------:|------:|--------:|------:|------:|------:|----------:|
    // | SharpZipLibCalculate |    .NET 4.7.2 |  1024 |   847.94 ns |   180.284 ns |   9.882 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  1024 |   458.80 ns |   146.235 ns |   8.016 ns |  0.54 |    0.02 |     - |     - |     - |         - |
    // |                      |               |       |             |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  1024 |   817.11 ns |    31.211 ns |   1.711 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  1024 |   421.48 ns |    86.149 ns |   4.722 ns |  0.52 |    0.01 |     - |     - |     - |         - |
    // |                      |               |       |             |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  1024 |   879.38 ns |    37.804 ns |   2.072 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  1024 |    57.27 ns |     2.008 ns |   0.110 ns |  0.07 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |             |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate |    .NET 4.7.2 |  2048 | 1,660.62 ns |    46.912 ns |   2.571 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  2048 |   938.41 ns | 3,137.008 ns | 171.950 ns |  0.57 |    0.10 |     - |     - |     - |         - |
    // |                      |               |       |             |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  2048 | 1,616.69 ns |   172.974 ns |   9.481 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  2048 |   871.52 ns |   485.678 ns |  26.622 ns |  0.54 |    0.02 |     - |     - |     - |         - |
    // |                      |               |       |             |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  2048 | 1,746.34 ns |   110.539 ns |   6.059 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  2048 |    96.31 ns |    24.491 ns |   1.342 ns |  0.06 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |             |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate |    .NET 4.7.2 |  4096 | 3,102.18 ns |   484.204 ns |  26.541 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  4096 | 1,729.49 ns |   104.446 ns |   5.725 ns |  0.56 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |             |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  4096 | 3,251.55 ns |   607.086 ns |  33.276 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  4096 | 1,669.22 ns |    25.194 ns |   1.381 ns |  0.51 |    0.01 |     - |     - |     - |         - |
    // |                      |               |       |             |              |            |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  4096 | 3,514.15 ns |   719.548 ns |  39.441 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  4096 |   180.12 ns |    55.425 ns |   3.038 ns |  0.05 |    0.00 |     - |     - |     - |         - |
}
