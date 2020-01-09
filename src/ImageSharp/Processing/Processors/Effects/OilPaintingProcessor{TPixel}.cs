// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Advanced.ParallelUtils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// Applies oil painting effect processing to the image.
    /// </summary>
    /// <remarks>Adapted from <see href="https://softwarebydefault.com/2013/06/29/oil-painting-cartoon-filter/"/> by Dewald Esterhuizen.</remarks>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class OilPaintingProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly OilPaintingProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="OilPaintingProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="OilPaintingProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public OilPaintingProcessor(Configuration configuration, OilPaintingProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            int brushSize = this.definition.BrushSize;
            if (brushSize <= 0 || brushSize > source.Height || brushSize > source.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(brushSize));
            }

            int startY = this.SourceRectangle.Y;
            int endY = this.SourceRectangle.Bottom;
            int startX = this.SourceRectangle.X;
            int endX = this.SourceRectangle.Right;
            int maxY = endY - 1;
            int maxX = endX - 1;

            int radius = brushSize >> 1;
            int levels = this.definition.Levels;

            using (Buffer2D<TPixel> targetPixels = this.Configuration.MemoryAllocator.Allocate2D<TPixel>(source.Size()))
            {
                source.CopyTo(targetPixels);

                var workingRect = Rectangle.FromLTRB(startX, startY, endX, endY);
                ParallelHelper.IterateRows(
                    workingRect,
                    this.Configuration,
                    rows =>
                        {
                            for (int y = rows.Min; y < rows.Max; y++)
                            {
                                Span<TPixel> sourceRow = source.GetPixelRowSpan(y);
                                Span<TPixel> targetRow = targetPixels.GetRowSpan(y);

                                for (int x = startX; x < endX; x++)
                                {
                                    int maxIntensity = 0;
                                    int maxIndex = 0;

                                    var intensityBin = new int[levels];
                                    var redBin = new float[levels];
                                    var blueBin = new float[levels];
                                    var greenBin = new float[levels];

                                    for (int fy = 0; fy <= radius; fy++)
                                    {
                                        int fyr = fy - radius;
                                        int offsetY = y + fyr;

                                        offsetY = offsetY.Clamp(0, maxY);

                                        Span<TPixel> sourceOffsetRow = source.GetPixelRowSpan(offsetY);

                                        for (int fx = 0; fx <= radius; fx++)
                                        {
                                            int fxr = fx - radius;
                                            int offsetX = x + fxr;
                                            offsetX = offsetX.Clamp(0, maxX);

                                            var vector = sourceOffsetRow[offsetX].ToVector4();

                                            float sourceRed = vector.X;
                                            float sourceBlue = vector.Z;
                                            float sourceGreen = vector.Y;

                                            int currentIntensity = (int)MathF.Round(
                                                (sourceBlue + sourceGreen + sourceRed) / 3F * (levels - 1));

                                            intensityBin[currentIntensity]++;
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
                                        pixel.FromVector4(
                                            new Vector4(red, green, blue, sourceRow[x].ToVector4().W));
                                    }
                                }
                            }
                        });

                Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
            }
        }
    }
}
