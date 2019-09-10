// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Applies a lightness filter matrix using
    /// </summary>
    public sealed class LightnessProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LightnessProcessor"/> class.
        /// </summary>
        /// <param name="lightness">Lightness of image in HSL color scheme</param>
        public LightnessProcessor(float lightness)
        : base(KnownFilterMatrices.CreateLightnessFilter(lightness))
        {
            this.Lightness = lightness;
        }

        /// <summary>
        /// Gets Lightness of image in HSL color scheme.
        /// The "brightness relative to the brightness of a similarly illuminated white" https://en.wikipedia.org/wiki/HSL_and_HSV#Lightness
        /// </summary>
        public float Lightness { get; }
    }
}
