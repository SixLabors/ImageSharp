// <copyright file="CopyPixels.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks.Image
{
    using System;
    using System.Threading.Tasks;

    using BenchmarkDotNet.Attributes;
    using SixLabors.ImageSharp.Advanced;
    using SixLabors.ImageSharp.Memory;

    public class CopyPixels : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "PixelAccessor Copy by indexer")]
        public Rgba32 CopyByPixelAccesor()
        {
            using (var source = new Image<Rgba32>(1024, 768))
            using (var target = new Image<Rgba32>(1024, 768))
            {
                using (PixelAccessor<Rgba32> sourcePixels = source.Lock())
                using (PixelAccessor<Rgba32> targetPixels = target.Lock())
                {
                    Parallel.For(
                        0,
                        source.Height,
                        Configuration.Default.ParallelOptions,
                        y =>
                            {
                                for (int x = 0; x < source.Width; x++)
                                {
                                    targetPixels[x, y] = sourcePixels[x, y];
                                }
                            });

                    return targetPixels[0, 0];
                }
            }
        }

        [Benchmark(Description = "PixelAccessor Copy by Span")]
        public Rgba32 CopyByPixelAccesorSpan()
        {
            using (var source = new Image<Rgba32>(1024, 768))
            using (var target = new Image<Rgba32>(1024, 768))
            {
                using (PixelAccessor<Rgba32> sourcePixels = source.Lock())
                using (PixelAccessor<Rgba32> targetPixels = target.Lock())
                {
                    Parallel.For(
                        0,
                        source.Height,
                        Configuration.Default.ParallelOptions,
                        y =>
                        {
                            Span<Rgba32> sourceRow = sourcePixels.GetRowSpan(y);
                            Span<Rgba32> targetRow = targetPixels.GetRowSpan(y);

                            for (int x = 0; x < source.Width; x++)
                            {
                                targetRow[x] = sourceRow[x];
                            }
                        });

                    return targetPixels[0, 0];
                }
            }
        }

        [Benchmark(Description = "Copy by indexer")]
        public Rgba32 Copy()
        {
            using (var source = new Image<Rgba32>(1024, 768))
            using (var target = new Image<Rgba32>(1024, 768))
            {
                Parallel.For(
                    0,
                    source.Height,
                    Configuration.Default.ParallelOptions,
                    y =>
                    {
                        for (int x = 0; x < source.Width; x++)
                        {
                            target[x, y] = source[x, y];
                        }
                    });

                return target[0, 0];
            }
        }

        [Benchmark(Description = "Copy by Span")]
        public Rgba32 CopySpan()
        {
            using (var source = new Image<Rgba32>(1024, 768))
            using (var target = new Image<Rgba32>(1024, 768))
            {
                Parallel.For(
                    0,
                    source.Height,
                    Configuration.Default.ParallelOptions,
                    y =>
                    {
                        Span<Rgba32> sourceRow = source.GetPixelRowSpan(y);
                        Span<Rgba32> targetRow = target.GetPixelRowSpan(y);

                        for (int x = 0; x < source.Width; x++)
                        {
                            targetRow[x] = sourceRow[x];
                        }
                    });

                return target[0, 0];
            }
        }
    }
}
