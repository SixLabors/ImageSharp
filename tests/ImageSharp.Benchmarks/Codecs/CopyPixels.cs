// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    public class CopyPixels : BenchmarkBase
    {
        [Benchmark(Baseline = true, Description = "PixelAccessor Copy by indexer")]
        public Rgba32 CopyByPixelAccesor()
        {
            using (var source = new Image<Rgba32>(1024, 768))
            using (var target = new Image<Rgba32>(1024, 768))
            {
                Buffer2D<Rgba32> sourcePixels = source.GetRootFramePixelBuffer();
                Buffer2D<Rgba32> targetPixels = target.GetRootFramePixelBuffer();
                ParallelFor.WithConfiguration(
                    0,
                    source.Height,
                    Configuration.Default,
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

        [Benchmark(Description = "PixelAccessor Copy by Span")]
        public Rgba32 CopyByPixelAccesorSpan()
        {
            using (var source = new Image<Rgba32>(1024, 768))
            using (var target = new Image<Rgba32>(1024, 768))
            {
                Buffer2D<Rgba32> sourcePixels = source.GetRootFramePixelBuffer();
                Buffer2D<Rgba32> targetPixels = target.GetRootFramePixelBuffer();
                ParallelFor.WithConfiguration(
                    0,
                    source.Height,
                    Configuration.Default,
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

        [Benchmark(Description = "Copy by indexer")]
        public Rgba32 Copy()
        {
            using (var source = new Image<Rgba32>(1024, 768))
            using (var target = new Image<Rgba32>(1024, 768))
            {
                ParallelFor.WithConfiguration(
                    0,
                    source.Height,
                    Configuration.Default,
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
                ParallelFor.WithConfiguration(
                    0,
                    source.Height,
                    Configuration.Default,
                    y =>
                        {
                            Span<Rgba32> sourceRow = source.Frames.RootFrame.GetPixelRowSpan(y);
                            Span<Rgba32> targetRow = target.Frames.RootFrame.GetPixelRowSpan(y);

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