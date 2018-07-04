// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Contains reusable static instances of known quantizing algorithms
    /// </summary>
    public static class KnownQuantizers
    {
        /// <summary>
        /// Gets the adaptive Octree quantizer. Fast with good quality.
        /// The quantizer only supports a single alpha value.
        /// </summary>
        public static IQuantizer Octree { get; } = new OctreeQuantizer();

        /// <summary>
        /// Gets the Xiaolin Wu's Color Quantizer which generates high quality output.
        /// The quantizer supports multiple alpha values.
        /// </summary>
        public static IQuantizer Wu { get; } = new WuQuantizer();

        /// <summary>
        /// Gets the palette based, Using the collection of web-safe colors.
        /// The quantizer supports multiple alpha values.
        /// </summary>
        public static IQuantizer Palette { get; } = new PaletteQuantizer();
    }
}