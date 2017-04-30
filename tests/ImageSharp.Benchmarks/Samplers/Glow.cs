// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks
{

    using BenchmarkDotNet.Attributes;
    using ImageSharp.PixelFormats;
    using ImageSharp.Drawing;
    using ImageSharp.Processing.Processors;
    using CoreImage = ImageSharp.Image;
    using CoreSize = ImageSharp.Size;
    using ImageSharp.Processing;
    using System.Numerics;
    using System;
    using System.Threading.Tasks;

    public class Glow : BenchmarkBase
    {
        private GlowProcessor<Rgba32> bulk;
        private GlowProcessorParallel<Rgba32> parallel;

        [Setup]
        public void Setup()
        {
            this.bulk = new GlowProcessor<Rgba32>(NamedColors<Rgba32>.Beige, GraphicsOptions.Default) { Radius = 800 * .5f, };
            this.parallel = new GlowProcessorParallel<Rgba32>(NamedColors<Rgba32>.Beige) { Radius = 800 * .5f, };

        }
        [Benchmark(Description = "ImageSharp Glow - Bulk")]
        public CoreSize GlowBulk()
        {
            using (CoreImage image = new CoreImage(800, 800))
            {
                image.ApplyProcessor(bulk, image.Bounds);
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "ImageSharp Glow - Parallel")]
        public CoreSize GLowSimple()
        {
            using (CoreImage image = new CoreImage(800, 800))
            {
                image.ApplyProcessor(parallel, image.Bounds);
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
            protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
            {
                int startY = sourceRectangle.Y;
                int endY = sourceRectangle.Bottom;
                int startX = sourceRectangle.X;
                int endX = sourceRectangle.Right;
                TPixel glowColor = this.GlowColor;
                Vector2 centre = Rectangle.Center(sourceRectangle).ToVector2();
                float maxDistance = this.Radius > 0 ? MathF.Min(this.Radius, sourceRectangle.Width * .5F) : sourceRectangle.Width * .5F;

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
                using (Buffer<TPixel> rowColors = new Buffer<TPixel>(width))
                using (PixelAccessor<TPixel> sourcePixels = source.Lock())
                {
                    for (int i = 0; i < width; i++)
                    {
                        rowColors[i] = glowColor;
                    }

                    Parallel.For(
                        minY,
                        maxY,
                        this.ParallelOptions,
                        y =>
                        {
                            int offsetY = y - startY;

                            for (int x = minX; x < maxX; x++)
                            {
                                int offsetX = x - startX;
                                float distance = Vector2.Distance(centre, new Vector2(offsetX, offsetY));
                                Vector4 sourceColor = sourcePixels[offsetX, offsetY].ToVector4();
                                TPixel packed = default(TPixel);
                                packed.PackFromVector4(Vector4BlendTransforms.PremultipliedLerp(sourceColor, glowColor.ToVector4(), 1 - (.95F * (distance / maxDistance))));
                                sourcePixels[offsetX, offsetY] = packed;
                            }
                        });
                }
            }
        }
    }
}
