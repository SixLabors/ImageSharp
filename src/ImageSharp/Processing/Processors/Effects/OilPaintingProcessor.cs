// <copyright file="OilPaintingProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;
    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// An <see cref="IImageProcessor{TPixel}"/> to apply an oil painting effect to an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <remarks>Adapted from <see href="https://softwarebydefault.com/2013/06/29/oil-painting-cartoon-filter/"/> by Dewald Esterhuizen.</remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class OilPaintingProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OilPaintingProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="levels">
        /// The number of intensity levels. Higher values result in a broader range of color intensities forming part of the result image.
        /// </param>
        /// <param name="brushSize">
        /// The number of neighboring pixels used in calculating each individual pixel value.
        /// </param>
        public OilPaintingProcessor(int levels, int brushSize)
        {
            Guard.MustBeGreaterThan(levels, 0, nameof(levels));
            Guard.MustBeGreaterThan(brushSize, 0, nameof(brushSize));

            this.Levels = levels;
            this.BrushSize = brushSize;
        }

        /// <summary>
        /// Gets the intensity levels
        /// </summary>
        public int Levels { get; }

        /// <summary>
        /// Gets the brush size
        /// </summary>
        public int BrushSize { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int radius = this.BrushSize >> 1;
            int levels = this.Levels;

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

            using (var targetPixels = new PixelAccessor<TPixel>(source.Width, source.Height))
            {
                source.CopyTo(targetPixels);

                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> sourceRow = source.GetRowSpan(y);
                        Span<TPixel> targetRow = targetPixels.GetRowSpan(y);

                        for (int x = startX; x < endX; x++)
                        {
                            int maxIntensity = 0;
                            int maxIndex = 0;

                            int[] intensityBin = new int[levels];
                            float[] redBin = new float[levels];
                            float[] blueBin = new float[levels];
                            float[] greenBin = new float[levels];

                            for (int fy = 0; fy <= radius; fy++)
                            {
                                int fyr = fy - radius;
                                int offsetY = y + fyr;

                                offsetY = offsetY.Clamp(0, maxY);

                                Span<TPixel> sourceOffsetRow = source.GetRowSpan(offsetY);

                                for (int fx = 0; fx <= radius; fx++)
                                {
                                    int fxr = fx - radius;
                                    int offsetX = x + fxr;
                                    offsetX = offsetX.Clamp(0, maxX);

                                    var vector = sourceOffsetRow[offsetX].ToVector4();

                                    float sourceRed = vector.X;
                                    float sourceBlue = vector.Z;
                                    float sourceGreen = vector.Y;

                                    int currentIntensity = (int)MathF.Round((sourceBlue + sourceGreen + sourceRed) / 3F * (levels - 1));

                                    intensityBin[currentIntensity] += 1;
                                    blueBin[currentIntensity] += sourceBlue;
                                    greenBin[currentIntensity] += sourceGreen;
                                    redBin[currentIntensity] += sourceRed;

                                    if (intensityBin[currentIntensity] > maxIntensity)
                                    {
                                        maxIntensity = intensityBin[currentIntensity];
                                        maxIndex = currentIntensity;
                                    }
                                }

                                float red = MathF.Abs(redBin[maxIndex] / maxIntensity);
                                float green = MathF.Abs(greenBin[maxIndex] / maxIntensity);
                                float blue = MathF.Abs(blueBin[maxIndex] / maxIntensity);

                                ref TPixel pixel = ref targetRow[x];
                                pixel.PackFromVector4(new Vector4(red, green, blue, sourceRow[x].ToVector4().W));
                            }
                        }
                    });

                source.SwapPixelsBuffers(targetPixels);
            }
        }
    }
}