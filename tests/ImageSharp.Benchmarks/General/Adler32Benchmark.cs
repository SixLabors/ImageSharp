// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png.Zlib;
using SharpAdler32 = ICSharpCode.SharpZipLib.Checksum.Adler32;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    [Config(typeof(Config.ShortMultiFramework))]
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
    // |               Method |       Runtime | Count |        Mean |       Error |    StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
    // |--------------------- |-------------- |------ |------------:|------------:|----------:|------:|--------:|------:|------:|------:|----------:|
    // | SharpZipLibCalculate |    .NET 4.7.2 |  1024 |   793.18 ns |   775.66 ns | 42.516 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  1024 |   384.86 ns |    15.64 ns |  0.857 ns |  0.49 |    0.03 |     - |     - |     - |         - |
    // |                      |               |       |             |             |           |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  1024 |   790.31 ns |   353.34 ns | 19.368 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  1024 |   465.28 ns |   652.41 ns | 35.761 ns |  0.59 |    0.03 |     - |     - |     - |         - |
    // |                      |               |       |             |             |           |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  1024 |   877.25 ns |    97.89 ns |  5.365 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  1024 |    45.60 ns |    13.28 ns |  0.728 ns |  0.05 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |             |             |           |       |         |       |       |       |           |
    // | SharpZipLibCalculate |    .NET 4.7.2 |  2048 | 1,537.04 ns |   428.44 ns | 23.484 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  2048 |   849.76 ns | 1,066.34 ns | 58.450 ns |  0.55 |    0.04 |     - |     - |     - |         - |
    // |                      |               |       |             |             |           |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  2048 | 1,616.97 ns |   276.70 ns | 15.167 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  2048 |   790.77 ns |   691.71 ns | 37.915 ns |  0.49 |    0.03 |     - |     - |     - |         - |
    // |                      |               |       |             |             |           |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  2048 | 1,735.11 ns | 1,374.22 ns | 75.325 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  2048 |    87.80 ns |    56.84 ns |  3.116 ns |  0.05 |    0.00 |     - |     - |     - |         - |
    // |                      |               |       |             |             |           |       |         |       |       |       |           |
    // | SharpZipLibCalculate |    .NET 4.7.2 |  4096 | 3,054.53 ns |   796.41 ns | 43.654 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate |    .NET 4.7.2 |  4096 | 1,538.90 ns |   487.02 ns | 26.695 ns |  0.50 |    0.01 |     - |     - |     - |         - |
    // |                      |               |       |             |             |           |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 2.1 |  4096 | 3,223.48 ns |    32.32 ns |  1.771 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 2.1 |  4096 | 1,547.60 ns |   309.72 ns | 16.977 ns |  0.48 |    0.01 |     - |     - |     - |         - |
    // |                      |               |       |             |             |           |       |         |       |       |       |           |
    // | SharpZipLibCalculate | .NET Core 3.1 |  4096 | 3,672.33 ns | 1,095.81 ns | 60.065 ns |  1.00 |    0.00 |     - |     - |     - |         - |
    // |   SixLaborsCalculate | .NET Core 3.1 |  4096 |   159.44 ns |    36.31 ns |  1.990 ns |  0.04 |    0.00 |     - |     - |     - |         - |
}
