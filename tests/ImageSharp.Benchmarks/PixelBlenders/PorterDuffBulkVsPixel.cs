// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks
{
    using System;

    using BenchmarkDotNet.Attributes;
    using SixLabors.ImageSharp.PixelFormats;
    using CoreSize = SixLabors.Primitives.Size;
    using System.Numerics;

    using SixLabors.ImageSharp.Memory;
    using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

    public class PorterDuffBulkVsPixel : BenchmarkBase
    {
        private void BulkVectorConvert<TPixel>(Span<TPixel> destination, Span<TPixel> background, Span<TPixel> source, Span<float> amount)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.MustBeGreaterThanOrEqualTo(background.Length, destination.Length, nameof(background.Length));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, destination.Length, nameof(source.Length));
            Guard.MustBeGreaterThanOrEqualTo(amount.Length, destination.Length, nameof(amount.Length));

            using (Buffer<Vector4> buffer = new Buffer<Vector4>(destination.Length * 3))
            {
                Span<Vector4> destinationSpan = buffer.Slice(0, destination.Length);
                Span<Vector4> backgroundSpan = buffer.Slice(destination.Length, destination.Length);
                Span<Vector4> sourceSpan = buffer.Slice(destination.Length * 2, destination.Length);

                PixelOperations<TPixel>.Instance.ToVector4(background, backgroundSpan, destination.Length);
                PixelOperations<TPixel>.Instance.ToVector4(source, sourceSpan, destination.Length);

                for (int i = 0; i < destination.Length; i++)
                {
                    destinationSpan[i] = PorterDuffFunctions.Normal(backgroundSpan[i], sourceSpan[i], amount[i]);
                }

                PixelOperations<TPixel>.Instance.PackFromVector4(destinationSpan, destination, destination.Length);
            }
        }
        private void BulkPixelConvert<TPixel>(Span<TPixel> destination, Span<TPixel> background, Span<TPixel> source, Span<float> amount)
           where TPixel : struct, IPixel<TPixel>
        {
            Guard.MustBeGreaterThanOrEqualTo(destination.Length, background.Length, nameof(destination));
            Guard.MustBeGreaterThanOrEqualTo(source.Length, background.Length, nameof(destination));
            Guard.MustBeGreaterThanOrEqualTo(amount.Length, background.Length, nameof(destination));

            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = PorterDuffFunctions.Normal(destination[i], source[i], amount[i]);
            }
        }

        [Benchmark(Description = "ImageSharp BulkVectorConvert")]
        public CoreSize BulkVectorConvert()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                Buffer<float> amounts = new Buffer<float>(image.Width);

                for (int x = 0; x < image.Width; x++)
                {
                    amounts[x] = 1;
                }
                using (PixelAccessor<Rgba32> pixels = image.Lock())
                {
                    for (int y = 0; y < image.Height; y++)
                    {
                        Span<Rgba32> span = pixels.GetRowSpan(y);
                        BulkVectorConvert(span, span, span, amounts);
                    }
                }
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "ImageSharp BulkPixelConvert")]
        public CoreSize BulkPixelConvert()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                Buffer<float> amounts = new Buffer<float>(image.Width);

                for (int x = 0; x < image.Width; x++)
                {
                    amounts[x] = 1;
                }
                using (PixelAccessor<Rgba32> pixels = image.Lock())
                {
                    for (int y = 0; y < image.Height; y++)
                    {
                        Span<Rgba32> span = pixels.GetRowSpan(y);
                        BulkPixelConvert(span, span, span, amounts);
                    }
                }
                return new CoreSize(image.Width, image.Height);
            }
        }
    }
}
