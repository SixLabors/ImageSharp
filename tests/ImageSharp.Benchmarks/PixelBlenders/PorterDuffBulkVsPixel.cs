// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

namespace SixLabors.ImageSharp.Benchmarks
{
    using CoreSize = SixLabors.Primitives.Size;

    public class PorterDuffBulkVsPixel : BenchmarkBase
    {
        private void BulkVectorConvert<TPixel>(
            Span<TPixel> destination,
            Span<TPixel> background,
            Span<TPixel> source,
            Span<float> amount)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.MustBeGreaterThanOrEqualTo(background.Length, destination.Length, nameof(background.Length));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, destination.Length, nameof(source.Length));
            Guard.MustBeGreaterThanOrEqualTo(amount.Length, destination.Length, nameof(amount.Length));

            using (IMemoryOwner<Vector4> buffer =
                Configuration.Default.MemoryAllocator.Allocate<Vector4>(destination.Length * 3))
            {
                Span<Vector4> destinationSpan = buffer.Slice(0, destination.Length);
                Span<Vector4> backgroundSpan = buffer.Slice(destination.Length, destination.Length);
                Span<Vector4> sourceSpan = buffer.Slice(destination.Length * 2, destination.Length);

                PixelOperations<TPixel>.Instance.ToVector4(background, backgroundSpan, destination.Length);
                PixelOperations<TPixel>.Instance.ToVector4(source, sourceSpan, destination.Length);

                for (int i = 0; i < destination.Length; i++)
                {
                    destinationSpan[i] = PorterDuffFunctions.NormalSrcOver(backgroundSpan[i], sourceSpan[i], amount[i]);
                }

                PixelOperations<TPixel>.Instance.PackFromVector4(destinationSpan, destination, destination.Length);
            }
        }

        private void BulkPixelConvert<TPixel>(
            Span<TPixel> destination,
            Span<TPixel> background,
            Span<TPixel> source,
            Span<float> amount)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.MustBeGreaterThanOrEqualTo(destination.Length, background.Length, nameof(destination));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, background.Length, nameof(destination));
            Guard.MustBeGreaterThanOrEqualTo(amount.Length, background.Length, nameof(destination));

            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = PorterDuffFunctions.NormalSrcOver(destination[i], source[i], amount[i]);
            }
        }

        [Benchmark(Description = "ImageSharp BulkVectorConvert")]
        public CoreSize BulkVectorConvert()
        {
            using (var image = new Image<Rgba32>(800, 800))
            {
                using (IMemoryOwner<float> amounts = Configuration.Default.MemoryAllocator.Allocate<float>(image.Width))
                {
                    amounts.GetSpan().Fill(1);

                    Buffer2D<Rgba32> pixels = image.GetRootFramePixelBuffer();
                    for (int y = 0; y < image.Height; y++)
                    {
                        Span<Rgba32> span = pixels.GetRowSpan(y);
                        this.BulkVectorConvert(span, span, span, amounts.GetSpan());
                    }

                    return new CoreSize(image.Width, image.Height);
                }
            }
        }

        [Benchmark(Description = "ImageSharp BulkPixelConvert")]
        public CoreSize BulkPixelConvert()
        {
            using (var image = new Image<Rgba32>(800, 800))
            {
                using (IMemoryOwner<float> amounts = Configuration.Default.MemoryAllocator.Allocate<float>(image.Width))
                {
                    amounts.GetSpan().Fill(1);
                    Buffer2D<Rgba32> pixels = image.GetRootFramePixelBuffer();
                    for (int y = 0; y < image.Height; y++)
                    {
                        Span<Rgba32> span = pixels.GetRowSpan(y);
                        this.BulkPixelConvert(span, span, span, amounts.GetSpan());
                    }

                    return new CoreSize(image.Width, image.Height);
                }
            }
        }
    }
}