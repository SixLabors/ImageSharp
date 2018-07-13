// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Color converter between LinearRgb and Rgb
    /// </summary>
    internal class LinearRgbToRgbConverter : IColorConversion<LinearRgb, Rgb>
    {
        /// <inheritdoc/>
        public Rgb Convert(in LinearRgb input)
        {
            float r = input.WorkingSpace.Companding.Compress(input.R);
            float g = input.WorkingSpace.Companding.Compress(input.G);
            float b = input.WorkingSpace.Companding.Compress(input.B);

            return new Rgb(r, g, b, input.WorkingSpace);
        }
    }
}