// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class ToVector4_Bgra32 : ToVector4<Bgra32>
    {
        [Benchmark(Baseline = true)]
        public void PixelOperations_Base()
        {
            new PixelOperations<Bgra32>().ToVector4(
                this.Configuration,
                this.source.GetSpan(),
                this.destination.GetSpan());
        }

        // RESULTS:
        //                       Method | Runtime | Count |       Mean |       Error |     StdDev | Scaled | ScaledSD |  Gen 0 | Allocated |
        // ---------------------------- |-------- |------ |-----------:|------------:|-----------:|-------:|---------:|-------:|----------:|
        //         PixelOperations_Base |     Clr |    64 |   339.9 ns |   138.30 ns |  7.8144 ns |   1.00 |     0.00 | 0.0072 |      24 B |
        //  PixelOperations_Specialized |     Clr |    64 |   338.1 ns |    13.30 ns |  0.7515 ns |   0.99 |     0.02 |      - |       0 B |
        //                              |         |       |            |             |            |        |          |        |           |
        //         PixelOperations_Base |    Core |    64 |   245.6 ns |    29.05 ns |  1.6413 ns |   1.00 |     0.00 | 0.0072 |      24 B |
        //  PixelOperations_Specialized |    Core |    64 |   257.1 ns |    37.89 ns |  2.1407 ns |   1.05 |     0.01 |      - |       0 B |
        //                              |         |       |            |             |            |        |          |        |           |
        //         PixelOperations_Base |     Clr |   256 |   972.7 ns |    61.98 ns |  3.5020 ns |   1.00 |     0.00 | 0.0057 |      24 B |
        //  PixelOperations_Specialized |     Clr |   256 |   882.9 ns |   126.21 ns |  7.1312 ns |   0.91 |     0.01 |      - |       0 B |
        //                              |         |       |            |             |            |        |          |        |           |
        //         PixelOperations_Base |    Core |   256 |   910.0 ns |    90.87 ns |  5.1346 ns |   1.00 |     0.00 | 0.0067 |      24 B |
        //  PixelOperations_Specialized |    Core |   256 |   448.4 ns |    15.77 ns |  0.8910 ns |   0.49 |     0.00 |      - |       0 B |
        //                              |         |       |            |             |            |        |          |        |           |
        //         PixelOperations_Base |     Clr |  2048 | 6,951.8 ns | 1,299.01 ns | 73.3963 ns |   1.00 |     0.00 |      - |      24 B |
        //  PixelOperations_Specialized |     Clr |  2048 | 5,852.3 ns |   630.56 ns | 35.6279 ns |   0.84 |     0.01 |      - |       0 B |
        //                              |         |       |            |             |            |        |          |        |           |
        //         PixelOperations_Base |    Core |  2048 | 6,937.5 ns | 1,692.19 ns | 95.6121 ns |   1.00 |     0.00 |      - |      24 B |
        //  PixelOperations_Specialized |    Core |  2048 | 2,994.5 ns | 1,126.65 ns | 63.6578 ns |   0.43 |     0.01 |      - |       0 B |
    }
}
