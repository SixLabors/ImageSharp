// <copyright file="PngColorType.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Provides enumeration of available png color types.
    /// </summary>
    public enum PngColorType
    {
        /// <summary>
        /// Each pixel is a grayscale sample.
        /// </summary>
        Grayscale = 0,

        /// <summary>
        /// Each pixel is an R,G,B triple.
        /// </summary>
        Rgb = 2,

        /// <summary>
        /// Each pixel is a palette index; a PLTE chunk must appear.
        /// </summary>
        Palette = 3,

        /// <summary>
        /// Each pixel is a grayscale sample, followed by an alpha sample.
        /// </summary>
        GrayscaleWithAlpha = 4,

        /// <summary>
        /// Each pixel is an R,G,B triple, followed by an alpha sample.
        /// </summary>
        RgbWithAlpha = 6
    }
}