// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OilPaintingFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The oil painting filter.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.Artistic
{
    using System;
    using System.Drawing;
    using System.Threading.Tasks;

    using ImageProcessor.Common.Extensions;

    /// <summary>
    /// The oil painting filter.
    /// </summary>
    public class OilPaintingFilter
    {
        /// <summary>
        /// The levels.
        /// </summary>
        private int levels;

        /// <summary>
        /// The brush size.
        /// </summary>
        private int brushSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="OilPaintingFilter"/> class.
        /// </summary>
        /// <param name="levels">
        /// The number of levels.
        /// </param>
        /// <param name="brushSize">
        /// The brush size.
        /// </param>
        public OilPaintingFilter(int levels, int brushSize)
        {
            this.levels = levels;
            this.brushSize = brushSize;
        }

        /// <summary>
        /// Gets or sets the number of levels.
        /// </summary>
        public int Levels
        {
            get
            {
                return this.levels;
            }

            set
            {
                if (value > 0)
                {
                    this.levels = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the brush size.
        /// </summary>
        public int BrushSize
        {
            get
            {
                return this.brushSize;
            }

            set
            {
                if (value > 0)
                {
                    this.brushSize = value;
                }
            }
        }

        /// <summary>
        /// Applies the oil painting filter. 
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <returns>
        /// The <see cref="Bitmap"/>.
        /// </returns>
        public Bitmap ApplyFilter(Bitmap source)
        {
            // TODO: Make this class implement an interface?
            int width = source.Width;
            int height = source.Height;

            int radius = this.brushSize >> 1;

            Bitmap destination = new Bitmap(width, height);
            destination.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            using (FastBitmap sourceBitmap = new FastBitmap(source))
            {
                using (FastBitmap destinationBitmap = new FastBitmap(destination))
                {
                    Parallel.For(
                        0,
                        height,
                        y =>
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int maxIntensity = 0;
                                int maxIndex = 0;
                                int[] intensityBin = new int[this.levels];
                                int[] blueBin = new int[this.levels];
                                int[] greenBin = new int[this.levels];
                                int[] redBin = new int[this.levels];
                                byte sourceAlpha = 255;

                                for (int i = 0; i <= radius; i++)
                                {
                                    int ir = i - radius;
                                    int offsetY = y + ir;

                                    // Skip the current row
                                    if (offsetY < 0)
                                    {
                                        continue;
                                    }

                                    // Outwith the current bounds so break.
                                    if (offsetY >= height)
                                    {
                                        break;
                                    }

                                    for (int fx = 0; fx <= radius; fx++)
                                    {
                                        int jr = fx - radius;
                                        int offsetX = x + jr;

                                        // Skip the column
                                        if (offsetX < 0)
                                        {
                                            continue;
                                        }

                                        if (offsetX < width)
                                        {
                                            // ReSharper disable once AccessToDisposedClosure
                                            Color color = sourceBitmap.GetPixel(offsetX, offsetY);

                                            byte sourceBlue = color.B;
                                            byte sourceGreen = color.G;
                                            byte sourceRed = color.R;
                                            sourceAlpha = color.A;

                                            int currentIntensity = (int)Math.Round(((sourceBlue + sourceGreen + sourceRed) / 3.0 * (this.levels - 1)) / 255.0);

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
                                }

                                byte blue = Math.Abs(blueBin[maxIndex] / maxIntensity).ToByte();
                                byte green = Math.Abs(greenBin[maxIndex] / maxIntensity).ToByte();
                                byte red = Math.Abs(redBin[maxIndex] / maxIntensity).ToByte();

                                // ReSharper disable once AccessToDisposedClosure
                                destinationBitmap.SetPixel(x, y, Color.FromArgb(sourceAlpha, red, green, blue));
                            }
                        });
                }
            }

            return destination;
        }
    }
}
