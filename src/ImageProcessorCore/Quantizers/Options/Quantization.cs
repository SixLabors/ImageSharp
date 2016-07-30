// <copyright file="Quantization.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Provides enumeration over how an image should be quantized.
    /// </summary>
    public enum Quantization
    {
        /// <summary>
        /// An adaptive Octree quantizer. Fast with good quality.
        /// </summary>
        Octree,

        /// <summary>
        /// Xiaolin Wu's Color Quantizer which generates high quality output.
        /// </summary>
        Wu,

        /// <summary>
        /// Palette based, Uses the collection of web-safe colors by default.
        /// </summary>
        Palette
    }
}
