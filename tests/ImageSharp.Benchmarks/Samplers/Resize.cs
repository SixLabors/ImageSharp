// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks
{
    [Config(typeof(Config.ShortClr))]
    public abstract class ResizeBenchmarkBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        protected readonly Configuration Configuration = new Configuration(new JpegConfigurationModule());

        private Image<TPixel> sourceImage;

        private Bitmap sourceBitmap;

        [Params(3032)]
        public int SourceSize { get; set; }

        [Params(400)]
        public int DestSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.sourceImage = new Image<TPixel>(this.Configuration, this.SourceSize, this.SourceSize);
            this.sourceBitmap = new Bitmap(this.SourceSize, this.SourceSize);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.sourceImage.Dispose();
            this.sourceBitmap.Dispose();
        }

        [Benchmark(Baseline = true)]
        public int SystemDrawing()
        {
            using (var destination = new Bitmap(this.DestSize, this.DestSize))
            {
                using (var g = Graphics.FromImage(destination))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;

                    g.DrawImage(this.sourceBitmap, 0, 0, this.DestSize, this.DestSize);
                }

                return destination.Width;
            }
        }

        [Benchmark(Description = "ImageSharp, MaxDegreeOfParallelism = 1")]
        public int ImageSharp_P1() => this.RunImageSharpResize(1);

        // Parallel cases have been disabled for fast benchmark execution.
        // Uncomment, if you are interested in parallel speedup

        //[Benchmark(Description = "ImageSharp, MaxDegreeOfParallelism = 4")]
        //public int ImageSharp_P4() => this.RunImageSharpResize(4);

        //[Benchmark(Description = "ImageSharp, MaxDegreeOfParallelism = 8")]
        //public int ImageSharp_P8() => this.RunImageSharpResize(8);

        protected int RunImageSharpResize(int maxDegreeOfParallelism)
        {
            this.Configuration.MaxDegreeOfParallelism = maxDegreeOfParallelism;

            using (Image<TPixel> clone = this.sourceImage.Clone(this.ExecuteResizeOperation))
            {
                return clone.Width;
            }
        }

        protected abstract void ExecuteResizeOperation(IImageProcessingContext<TPixel> ctx);
    }

    public class Resize_Bicubic_Rgba32 : ResizeBenchmarkBase<Rgba32>
    {
        protected override void ExecuteResizeOperation(IImageProcessingContext<Rgba32> ctx)
        {
            ctx.Resize(this.DestSize, this.DestSize, KnownResamplers.Bicubic);
        }

        // RESULTS (2018 October):
        //
        // BenchmarkDotNet=v0.10.14, OS=Windows 10.0.17134
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742191 Hz, Resolution=364.6719 ns, Timer=TSC
        // .NET Core SDK=2.1.403
        //   [Host]     : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT
        //   Job-IGUFBA : .NET Framework 4.7.1 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3190.0
        //   Job-DZFERG : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT
        //
        //                                    Method | Runtime | SourceSize | DestSize |      Mean |     Error |    StdDev | Scaled | ScaledSD | Allocated |
        // ----------------------------------------- |-------- |----------- |--------- |----------:|----------:|----------:|-------:|---------:|----------:|
        //                             SystemDrawing |     Clr |       3032 |      400 | 101.13 ms | 18.659 ms | 1.0542 ms |   1.00 |     0.00 |       0 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' |     Clr |       3032 |      400 | 122.05 ms | 19.622 ms | 1.1087 ms |   1.21 |     0.01 |   21856 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 4' |     Clr |       3032 |      400 |  41.34 ms | 54.841 ms | 3.0986 ms |   0.41 |     0.03 |   28000 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 8' |     Clr |       3032 |      400 |  31.68 ms | 12.782 ms | 0.7222 ms |   0.31 |     0.01 |   28256 B |
        //                                           |         |            |          |           |           |           |        |          |           |
        //                             SystemDrawing |    Core |       3032 |      400 | 100.37 ms | 18.479 ms | 1.0441 ms |   1.00 |     0.00 |       0 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' |    Core |       3032 |      400 |  73.03 ms | 10.540 ms | 0.5955 ms |   0.73 |     0.01 |   21368 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 4' |    Core |       3032 |      400 |  22.59 ms |  4.863 ms | 0.2748 ms |   0.23 |     0.00 |   25220 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 8' |    Core |       3032 |      400 |  21.10 ms | 23.362 ms | 1.3200 ms |   0.21 |     0.01 |   25539 B |

    }

    public class Resize_Bicubic_Bgra32 : ResizeBenchmarkBase<Bgra32>
    {
        protected override void ExecuteResizeOperation(IImageProcessingContext<Bgra32> ctx)
        {
            ctx.Resize(this.DestSize, this.DestSize, KnownResamplers.Bicubic);
        }
    }

    public class Resize_BicubicCompand_Rgba32 : ResizeBenchmarkBase<Rgba32>
    {
        protected override void ExecuteResizeOperation(IImageProcessingContext<Rgba32> ctx)
        {
            ctx.Resize(this.DestSize, this.DestSize, KnownResamplers.Bicubic, true);
        }

        // RESULTS (2018 October):
        //
        // BenchmarkDotNet=v0.10.14, OS=Windows 10.0.17134
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742191 Hz, Resolution=364.6719 ns, Timer=TSC
        // .NET Core SDK=2.1.403
        //   [Host]     : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT
        //   Job-IGUFBA : .NET Framework 4.7.1 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3190.0
        //   Job-DZFERG : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT
        //
        //                                    Method | Runtime | SourceSize | DestSize |      Mean |     Error |    StdDev | Scaled | ScaledSD | Allocated |
        // ----------------------------------------- |-------- |----------- |--------- |----------:|----------:|----------:|-------:|---------:|----------:|
        //                             SystemDrawing |     Clr |       3032 |      400 | 100.63 ms | 13.864 ms | 0.7833 ms |   1.00 |     0.00 |       0 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' |     Clr |       3032 |      400 | 156.83 ms | 28.631 ms | 1.6177 ms |   1.56 |     0.02 |   21856 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 4' |     Clr |       3032 |      400 |  53.43 ms | 38.493 ms | 2.1749 ms |   0.53 |     0.02 |   28512 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 8' |     Clr |       3032 |      400 |  38.47 ms | 11.969 ms | 0.6763 ms |   0.38 |     0.01 |   28000 B |
        //                                           |         |            |          |           |           |           |        |          |           |
        //                             SystemDrawing |    Core |       3032 |      400 |  99.87 ms | 23.459 ms | 1.3255 ms |   1.00 |     0.00 |       0 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' |    Core |       3032 |      400 | 108.19 ms | 38.562 ms | 2.1788 ms |   1.08 |     0.02 |   21368 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 4' |    Core |       3032 |      400 |  36.21 ms | 53.802 ms | 3.0399 ms |   0.36 |     0.03 |   25300 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 8' |    Core |       3032 |      400 |  26.52 ms |  2.173 ms | 0.1228 ms |   0.27 |     0.00 |   25589 B |
    }
}
