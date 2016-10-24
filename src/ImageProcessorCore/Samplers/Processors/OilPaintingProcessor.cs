// <copyright file="OilPaintingProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageSampler{TColor,TPacked}"/> to apply an oil painting effect to an <see cref="Image{TColor, TPacked}"/>.
    /// </summary>
    /// <remarks>Adapted from <see href="https://softwarebydefault.com/2013/06/29/oil-painting-cartoon-filter/"/> by Dewald Esterhuizen.</remarks>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class OilPaintingProcessor<TColor, TPacked> : ImageSampler<TColor, TPacked>
        where TColor : struct, IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OilPaintingProcessor{T,TP}"/> class.
        /// </summary>
        /// <param name="levels">The number of intensity levels. Higher values result in a broader range of colour intensities forming part of the result image.</param>
        /// <param name="brushSize">The number of neighbouring pixels used in calculating each individual pixel value.</param>
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
        public override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
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

            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                    {
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

                                // Skip the current row
                                if (offsetY < minY)
                                {
                                    continue;
                                }

                                // Outwith the current bounds so break.
                                if (offsetY >= maxY)
                                {
                                    break;
                                }

                                for (int fx = 0; fx <= radius; fx++)
                                {
                                    int fxr = fx - radius;
                                    int offsetX = x + fxr;

                                    // Skip the column
                                    if (offsetX < 0)
                                    {
                                        continue;
                                    }

                                    if (offsetX < maxX)
                                    {
                                        // ReSharper disable once AccessToDisposedClosure
                                        Vector4 color = sourcePixels[offsetX, offsetY].ToVector4();

                                        float sourceRed = color.X;
                                        float sourceBlue = color.Z;
                                        float sourceGreen = color.Y;

                                        int currentIntensity = (int)Math.Round(((sourceBlue + sourceGreen + sourceRed) / 3.0 * (levels - 1)));

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
                                }

                                float red = Math.Abs(redBin[maxIndex] / maxIntensity);
                                float green = Math.Abs(greenBin[maxIndex] / maxIntensity);
                                float blue = Math.Abs(blueBin[maxIndex] / maxIntensity);

                                Vector4 targetColor = targetPixels[x, y].ToVector4();
                                TColor packed = default(TColor);
                                packed.PackFromVector4(new Vector4(red, green, blue, targetColor.Z));
                                targetPixels[x, y] = packed;
                            }

                        }
                    });
            }
        }
    }
}