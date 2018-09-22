// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Overlays;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Benchmarks
{
    using CoreSize = SixLabors.Primitives.Size;

    public class Glow : BenchmarkBase
    {
        private GlowProcessor<Rgba32> bulk;

        private GlowProcessorParallel<Rgba32> parallel;

        [GlobalSetup]
        public void Setup()
        {
            this.bulk = new GlowProcessor<Rgba32>(NamedColors<Rgba32>.Beige, 800 * .5f, GraphicsOptions.Default);
            this.parallel = new GlowProcessorParallel<Rgba32>(NamedColors<Rgba32>.Beige) { Radius = 800 * .5f, };
        }

        [Benchmark(Description = "ImageSharp Glow - Bulk")]
        public CoreSize GlowBulk()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                this.bulk.Apply(image, image.Bounds());
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "ImageSharp Glow - Parallel")]
        public CoreSize GLowSimple()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(800, 800))
            {
                this.parallel.Apply(image, image.Bounds());
                return new CoreSize(image.Width, image.Height);
            }
        }

        internal class GlowProcessorParallel<TPixel> : ImageProcessor<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="GlowProcessorParallel{TPixel}" /> class.
            /// </summary>
            /// <param name="color">The color or the glow.</param>
            public GlowProcessorParallel(TPixel color)
            {
                this.GlowColor = color;
            }

            /// <summary>
            /// Gets or sets the glow color to apply.
            /// </summary>
            public TPixel GlowColor { get; set; }

            /// <summary>
            /// Gets or sets the the radius.
            /// </summary>
            public float Radius { get; set; }

            /// <inheritdoc/>
            protected override void OnFrameApply(
                ImageFrame<TPixel> source,
                Rectangle sourceRectangle,
                Configuration configuration)
            {
                int startY = sourceRectangle.Y;
                int endY = sourceRectangle.Bottom;
                int startX = sourceRectangle.X;
                int endX = sourceRectangle.Right;
                TPixel glowColor = this.GlowColor;
                Vector2 centre = Rectangle.Center(sourceRectangle);
                float maxDistance = this.Radius > 0
                                        ? Math.Min(this.Radius, sourceRectangle.Width * .5F)
                                        : sourceRectangle.Width * .5F;

                // Align start/end positions.
                int minX = Math.Max(0, startX);
                int maxX = Math.Min(source.Width, endX);
                int minY = Math.Max(0, startY);
                int maxY = Math.Min(source.Height, endY);

                // Reset offset if necessary.
                if (minX > 0)
                {
                    startX = 0;
                }

                if (minY > 0)
                {
                    startY = 0;
                }

                int width = maxX - minX;
                using (IMemoryOwner<TPixel> rowColors = Configuration.Default.MemoryAllocator.Allocate<TPixel>(width))
                {
                    Buffer2D<TPixel> sourcePixels = source.PixelBuffer;
                    rowColors.GetSpan().Fill(glowColor);

                    var workingRect = Rectangle.FromLTRB(minX, minY, maxX, maxY);
                    ParallelHelper.IterateRows(
                        workingRect,
                        configuration,
                        rows =>
                            {
                                for (int y = rows.Min; y < rows.Max; y++)
                                {
                                    int offsetY = y - startY;

                                    for (int x = minX; x < maxX; x++)
                                    {
                                        int offsetX = x - startX;
                                        float distance = Vector2.Distance(centre, new Vector2(offsetX, offsetY));
                                        Vector4 sourceColor = sourcePixels[offsetX, offsetY].ToVector4();
                                        TPixel packed = default(TPixel);
                                        packed.PackFromVector4(
                                            PremultipliedLerp(
                                                sourceColor,
                                                glowColor.ToVector4(),
                                                1 - (.95F * (distance / maxDistance))));
                                        sourcePixels[offsetX, offsetY] = packed;
                                    }
                                }
                            });
                }
            }

            public static Vector4 PremultipliedLerp(Vector4 backdrop, Vector4 source, float amount)
            {
                amount = amount.Clamp(0, 1);

                // Santize on zero alpha
                if (Math.Abs(backdrop.W) < Constants.Epsilon)
                {
                    source.W *= amount;
                    return source;
                }

                if (Math.Abs(source.W) < Constants.Epsilon)
                {
                    return backdrop;
                }

                // Premultiply the source vector.
                // Oddly premultiplying the background vector creates dark outlines when pixels
                // Have low alpha values.
                source = new Vector4(source.X, source.Y, source.Z, 1) * (source.W * amount);

                // This should be implementing the following formula
                // https://en.wikipedia.org/wiki/Alpha_compositing
                // Vout =  Vs + Vb (1 - Vsa)
                // Aout = Vsa + Vsb (1 - Vsa)
                Vector3 inverseW = new Vector3(1 - source.W);
                Vector3 xyzB = new Vector3(backdrop.X, backdrop.Y, backdrop.Z);
                Vector3 xyzS = new Vector3(source.X, source.Y, source.Z);

                return new Vector4(xyzS + (xyzB * inverseW), source.W + (backdrop.W * (1 - source.W)));
            }
        }
    }
}