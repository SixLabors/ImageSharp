// <copyright file="Quantization.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Provides enumeration over how an image should be quantized.
    /// </summary>
    public enum Quantization
    {
        /// <summary>
        /// An adaptive Octree quantizer. Fast with good quality.
        /// The quantizer only supports a single alpha value.
        /// </summary>
        Octree,

        /// <summary>
        /// Xiaolin Wu's Color Quantizer which generates high quality output.
        /// The quantizer supports multiple alpha values.
        /// </summary>
        Wu,

        /// <summary>
        /// Palette based, Uses the collection of web-safe colors by default.
        /// The quantizer supports multiple alpha values.
        /// </summary>
        Palette
    }
}