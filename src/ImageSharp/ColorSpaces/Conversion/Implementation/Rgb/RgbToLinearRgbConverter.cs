// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Color converter between Rgb and LinearRgb
    /// </summary>
    internal class RgbToLinearRgbConverter : IColorConversion<Rgb, LinearRgb>
    {
        /// <inheritdoc/>
        public LinearRgb Convert(in Rgb input)
        {
            float r = input.WorkingSpace.Companding.Expand(input.R);
            float g = input.WorkingSpace.Companding.Expand(input.G);
            float b = input.WorkingSpace.Companding.Expand(input.B);

            return new LinearRgb(r, g, b, input.WorkingSpace);
        }
    }
}