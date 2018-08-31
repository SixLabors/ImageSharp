// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

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
            Vector3 vector = input.Vector;
            vector.X = input.WorkingSpace.Companding.Expand(vector.X);
            vector.Y = input.WorkingSpace.Companding.Expand(vector.Y);
            vector.Z = input.WorkingSpace.Companding.Expand(vector.Z);

            return new LinearRgb(vector, input.WorkingSpace);
        }
    }
}