// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks
{
    [Config(typeof(Config.MultiFramework))]
    public abstract class Resize<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private byte[] bytes = null;

        private Image<TPixel> sourceImage;

        private SDImage sourceBitmap;

        protected Configuration Configuration { get; } = new Configuration(new JpegConfigurationModule());

        protected int DestSize { get; private set; }

        [GlobalSetup]
        public virtual void Setup()
        {
            if (this.bytes is null)
            {
                this.bytes = File.ReadAllBytes(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Jpeg.Baseline.Snake));

                this.sourceImage = Image.Load<TPixel>(this.bytes);

                var ms1 = new MemoryStream(this.bytes);
                this.sourceBitmap = SDImage.FromStream(ms1);
                this.DestSize = this.sourceBitmap.Width / 2;
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.bytes = null;
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

        /*
        [Benchmark(Description = "ImageSharp, MaxDegreeOfParallelism = 4")]
        public int ImageSharp_P4() => this.RunImageSharpResize(4);

        [Benchmark(Description = "ImageSharp, MaxDegreeOfParallelism = 8")]
         public int ImageSharp_P8() => this.RunImageSharpResize(8);
        */

        protected int RunImageSharpResize(int maxDegreeOfParallelism)
        {
            this.Configuration.MaxDegreeOfParallelism = maxDegreeOfParallelism;

            using (Image<TPixel> clone = this.sourceImage.Clone(this.ExecuteResizeOperation))
            {
                return clone.Width;
            }
        }

        protected abstract void ExecuteResizeOperation(IImageProcessingContext ctx);
    }

    public class Resize_Bicubic_Rgba32 : Resize<Rgba32>
    {
        protected override void ExecuteResizeOperation(IImageProcessingContext ctx)
            => ctx.Resize(this.DestSize, this.DestSize, KnownResamplers.Bicubic);

        // RESULTS - 2019 April - ResizeWorker:
        //
        // BenchmarkDotNet=v0.11.3, OS=Windows 10.0.17134.706 (1803/April2018Update/Redstone4)
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742189 Hz, Resolution=364.6722 ns, Timer=TSC
        // .NET Core SDK=2.2.202
        //   [Host] : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //   Clr    : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3394.0
        //   Core   : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //
        // IterationCount=3  LaunchCount=1  WarmupCount=3
        //
        //                                    Method |  Job | Runtime | SourceToDest |      Mean |     Error |    StdDev | Ratio | RatioSD | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
        // ----------------------------------------- |----- |-------- |------------- |----------:|----------:|----------:|------:|--------:|------------:|------------:|------------:|--------------------:|
        //                             SystemDrawing |  Clr |     Clr |     3032-400 | 120.11 ms |  1.435 ms | 0.0786 ms |  1.00 |    0.00 |           - |           - |           - |              1638 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' |  Clr |     Clr |     3032-400 |  75.32 ms | 34.143 ms | 1.8715 ms |  0.63 |    0.02 |           - |           - |           - |             16384 B |
        //                                           |      |         |              |           |           |           |       |         |             |             |             |                     |
        //                             SystemDrawing | Core |    Core |     3032-400 | 120.33 ms |  6.669 ms | 0.3656 ms |  1.00 |    0.00 |           - |           - |           - |                96 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' | Core |    Core |     3032-400 |  88.56 ms |  1.864 ms | 0.1022 ms |  0.74 |    0.00 |           - |           - |           - |             15568 B |
    }

    /// <summary>
    /// Is it worth to set a larger working buffer limit for resize?
    /// Conclusion: It doesn't really have an effect.
    /// </summary>
    public class Resize_Bicubic_Rgba32_CompareWorkBufferSizes : Resize_Bicubic_Rgba32
    {
        [Params(128, 512, 1024, 8 * 1024)]
        public int WorkingBufferSizeHintInKilobytes { get; set; }

        public override void Setup()
        {
            this.Configuration.WorkingBufferSizeHintInBytes = this.WorkingBufferSizeHintInKilobytes * 1024;
            base.Setup();
        }
    }

    public class Resize_Bicubic_Bgra32 : Resize<Bgra32>
    {
        protected override void ExecuteResizeOperation(IImageProcessingContext ctx)
            => ctx.Resize(this.DestSize, this.DestSize, KnownResamplers.Bicubic);

        // RESULTS (2019 April):
        //
        // BenchmarkDotNet=v0.11.3, OS=Windows 10.0.17134.648 (1803/April2018Update/Redstone4)
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742192 Hz, Resolution=364.6718 ns, Timer=TSC
        // .NET Core SDK=2.1.602
        //   [Host] : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //   Clr    : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3362.0
        //   Core   : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //
        // IterationCount=3  LaunchCount=1  WarmupCount=3
        //
        //                                    Method |  Job | Runtime | SourceSize | DestSize |      Mean |     Error |    StdDev | Ratio | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
        // ----------------------------------------- |----- |-------- |----------- |--------- |----------:|----------:|----------:|------:|------------:|------------:|------------:|--------------------:|
        //                             SystemDrawing |  Clr |     Clr |       3032 |      400 | 119.01 ms | 18.513 ms | 1.0147 ms |  1.00 |           - |           - |           - |              1638 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' |  Clr |     Clr |       3032 |      400 | 104.71 ms | 16.078 ms | 0.8813 ms |  0.88 |           - |           - |           - |             45056 B |
        //                                           |      |         |            |          |           |           |           |       |             |             |             |                     |
        //                             SystemDrawing | Core |    Core |       3032 |      400 | 121.58 ms | 50.084 ms | 2.7453 ms |  1.00 |           - |           - |           - |                96 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' | Core |    Core |       3032 |      400 |  96.96 ms |  7.899 ms | 0.4329 ms |  0.80 |           - |           - |           - |             44512 B |
    }

    public class Resize_Bicubic_Rgb24 : Resize<Rgb24>
    {
        protected override void ExecuteResizeOperation(IImageProcessingContext ctx)
            => ctx.Resize(this.DestSize, this.DestSize, KnownResamplers.Bicubic);

        // RESULTS (2019 April):
        //
        // BenchmarkDotNet=v0.11.3, OS=Windows 10.0.17134.648 (1803/April2018Update/Redstone4)
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742192 Hz, Resolution=364.6718 ns, Timer=TSC
        // .NET Core SDK=2.1.602
        //   [Host] : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //   Clr    : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3362.0
        //   Core   : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //
        //                                    Method |  Job | Runtime | SourceSize | DestSize |      Mean |     Error |    StdDev | Ratio | RatioSD | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
        // ----------------------------------------- |----- |-------- |----------- |--------- |----------:|----------:|----------:|------:|--------:|------------:|------------:|------------:|--------------------:|
        //                             SystemDrawing |  Clr |     Clr |       3032 |      400 | 121.37 ms | 48.580 ms | 2.6628 ms |  1.00 |    0.00 |           - |           - |           - |              2048 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' |  Clr |     Clr |       3032 |      400 |  99.36 ms | 11.356 ms | 0.6224 ms |  0.82 |    0.02 |           - |           - |           - |             45056 B |
        //                                           |      |         |            |          |           |           |           |       |         |             |             |             |                     |
        //                             SystemDrawing | Core |    Core |       3032 |      400 | 118.06 ms | 15.667 ms | 0.8587 ms |  1.00 |    0.00 |           - |           - |           - |                96 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' | Core |    Core |       3032 |      400 |  92.47 ms |  5.683 ms | 0.3115 ms |  0.78 |    0.01 |           - |           - |           - |             44512 B |
    }

    public class Resize_BicubicCompand_Rgba32 : Resize<Rgba32>
    {
        protected override void ExecuteResizeOperation(IImageProcessingContext ctx)
            => ctx.Resize(this.DestSize, this.DestSize, KnownResamplers.Bicubic, true);

        // RESULTS (2019 April):
        //
        // BenchmarkDotNet=v0.11.3, OS=Windows 10.0.17134.648 (1803/April2018Update/Redstone4)
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742192 Hz, Resolution=364.6718 ns, Timer=TSC
        // .NET Core SDK=2.1.602
        //   [Host] : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //   Clr    : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3362.0
        //   Core   : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //
        // IterationCount=3  LaunchCount=1  WarmupCount=3
        //
        //                                    Method |  Job | Runtime | SourceSize | DestSize |     Mean |     Error |    StdDev | Ratio | RatioSD | Gen 0/1k Op | Gen 1/1k Op | Gen 2/1k Op | Allocated Memory/Op |
        // ----------------------------------------- |----- |-------- |----------- |--------- |---------:|----------:|----------:|------:|--------:|------------:|------------:|------------:|--------------------:|
        //                             SystemDrawing |  Clr |     Clr |       3032 |      400 | 120.7 ms | 68.985 ms | 3.7813 ms |  1.00 |    0.00 |           - |           - |           - |              1638 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' |  Clr |     Clr |       3032 |      400 | 132.2 ms | 15.976 ms | 0.8757 ms |  1.10 |    0.04 |           - |           - |           - |             16384 B |
        //                                           |      |         |            |          |          |           |           |       |         |             |             |             |                     |
        //                             SystemDrawing | Core |    Core |       3032 |      400 | 118.3 ms |  6.899 ms | 0.3781 ms |  1.00 |    0.00 |           - |           - |           - |                96 B |
        //  'ImageSharp, MaxDegreeOfParallelism = 1' | Core |    Core |       3032 |      400 | 122.4 ms | 15.069 ms | 0.8260 ms |  1.03 |    0.01 |           - |           - |           - |             15712 B |
    }
}
