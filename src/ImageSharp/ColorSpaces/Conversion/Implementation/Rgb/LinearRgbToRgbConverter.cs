// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce
{
    /// <summary>
    /// Color converter between LinearRgb and Rgb
    /// </summary>
    internal class LinearRgbToRgbConverter : IColorConversion<LinearRgb, Rgb>
    {
        /// <inheritdoc/>
        public Rgb Convert(LinearRgb input)
        {
            DebugGuard.NotNull(input, nameof(input));

            Vector3 vector = input.Vector;
            vector.X = input.WorkingSpace.Compress(vector.X);
            vector.Y = input.WorkingSpace.Compress(vector.Y);
            vector.Z = input.WorkingSpace.Compress(vector.Z);

            return new Rgb(vector, input.WorkingSpace);
        }
    }
}