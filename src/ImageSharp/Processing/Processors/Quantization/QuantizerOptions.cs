// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Dithering;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Defines options for quantization.
    /// </summary>
    public class QuantizerOptions
    {
        private float ditherScale = QuantizerConstants.MaxDitherScale;
        private int maxColors = QuantizerConstants.MaxColors;

        /// <summary>
        /// Gets or sets the  algorithm to apply to the output image.
        /// Defaults to <see cref="QuantizerConstants.DefaultDither"/>; set to <see langword="null"/> for no dithering.
        /// </summary>
        public IDither Dither { get; set; } = QuantizerConstants.DefaultDither;

        /// <summary>
        /// Gets or sets the dithering scale used to adjust the amount of dither. Range 0..1.
        /// Defaults to <see cref="QuantizerConstants.MaxDitherScale"/>.
        /// </summary>
        public float DitherScale
        {
            get { return this.ditherScale; }
            set { this.ditherScale = Numerics.Clamp(value, QuantizerConstants.MinDitherScale, QuantizerConstants.MaxDitherScale); }
        }

        /// <summary>
        /// Gets or sets the maximum number of colors to hold in the color palette. Range 0..256.
        /// Defaults to <see cref="QuantizerConstants.MaxColors"/>.
        /// </summary>
        public int MaxColors
        {
            get { return this.maxColors; }
            set { this.maxColors = Numerics.Clamp(value, QuantizerConstants.MinColors, QuantizerConstants.MaxColors); }
        }
    }
}
