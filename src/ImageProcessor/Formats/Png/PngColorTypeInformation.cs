// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PngColorTypeInformation.cs" company="James Jackson-South">
//   Copyright (c) James Jackson-South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Contains information that are required when loading a png with a specific color type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    using System;

    /// <summary>
    /// Contains information that are required when loading a png with a specific color type.
    /// </summary>
    internal sealed class PngColorTypeInformation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PngColorTypeInformation"/> class with 
        /// the scanline factory, the function to create the color reader and the supported bit depths.
        /// </summary>
        /// <param name="scanlineFactor">The scanline factor.</param>
        /// <param name="supportedBitDepths">The supported bit depths.</param>
        /// <param name="scanlineReaderFactory">The factory to create the color reader.</param>
        public PngColorTypeInformation(int scanlineFactor, int[] supportedBitDepths, Func<byte[], byte[], IColorReader> scanlineReaderFactory)
        {
            this.ChannelsPerColor = scanlineFactor;
            this.ScanlineReaderFactory = scanlineReaderFactory;
            this.SupportedBitDepths = supportedBitDepths;
        }

        /// <summary>
        /// Gets an array with the bit depths that are supported for the color type
        /// where this object is created for.
        /// </summary>
        /// <value>The supported bit depths that can be used in combination with this color type.</value>
        public int[] SupportedBitDepths { get; private set; }

        /// <summary>
        /// Gets a function that is used the create the color reader for the color type where 
        /// this object is created for.
        /// </summary>
        /// <value>The factory function to create the color type.</value>
        public Func<byte[], byte[], IColorReader> ScanlineReaderFactory { get; private set; }

        /// <summary>
        /// Gets a factor that is used when iterating through the scan lines.
        /// </summary>
        /// <value>The scanline factor.</value>
        public int ChannelsPerColor { get; private set; }

        /// <summary>
        /// Creates the color reader for the color type where this object is create for.
        /// </summary>
        /// <param name="palette">The palette of the image. Can be null when no palette is used.</param>
        /// <param name="paletteAlpha">The alpha palette of the image. Can be null when 
        /// no palette is used for the image or when the image has no alpha.</param>
        /// <returns>The color reader for the image.</returns>
        public IColorReader CreateColorReader(byte[] palette, byte[] paletteAlpha)
        {
            return this.ScanlineReaderFactory(palette, paletteAlpha);
        }
    }
}
