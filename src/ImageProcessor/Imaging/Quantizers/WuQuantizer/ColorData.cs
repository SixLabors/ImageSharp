// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorData.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace nQuant
{
    /// <summary>
    /// The color data.
    /// </summary>
    public class ColorData
    {
        /// <summary>
        /// The pixels.
        /// </summary>
        private readonly Pixel[] pixels;

        /// <summary>
        /// The pixels count.
        /// </summary>
        private readonly int pixelsCount;

        /// <summary>
        /// The pixel filling counter.
        /// </summary>
        private int pixelFillingCounter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorData"/> class.
        /// </summary>
        /// <param name="dataGranularity">
        /// The data granularity.
        /// </param>
        /// <param name="bitmapWidth">
        /// The bitmap width.
        /// </param>
        /// <param name="bitmapHeight">
        /// The bitmap height.
        /// </param>
        public ColorData(int dataGranularity, int bitmapWidth, int bitmapHeight)
        {
            dataGranularity++;

            this.Moments = new ColorMoment[dataGranularity, dataGranularity, dataGranularity, dataGranularity];
            this.pixelsCount = bitmapWidth * bitmapHeight;
            this.pixels = new Pixel[this.pixelsCount];
        }

        /// <summary>
        /// Gets the moments.
        /// </summary>
        public ColorMoment[, , ,] Moments { get; private set; }

        /// <summary>
        /// Gets the pixels.
        /// </summary>
        public Pixel[] Pixels
        {
            get
            {
                return this.pixels;
            }
        }

        /// <summary>
        /// Gets the pixels count.
        /// </summary>
        public int PixelsCount
        {
            get
            {
                return this.pixelsCount;
            }
        }

        /// <summary>
        /// The add pixel.
        /// </summary>
        /// <param name="pixel">
        /// The pixel.
        /// </param>
        /// <param name="quantizedPixel">
        /// The quantized pixel.
        /// </param>
        public void AddPixel(Pixel pixel, Pixel quantizedPixel)
        {
            this.pixels[this.pixelFillingCounter++] = pixel;
        }
    }
}