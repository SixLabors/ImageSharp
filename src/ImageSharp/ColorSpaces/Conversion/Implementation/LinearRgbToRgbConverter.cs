// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation
{
    /// <summary>
    /// Color converter between LinearRgb and Rgb
    /// </summary>
    internal sealed class LinearRgbToRgbConverter : IColorConversion<LinearRgb, Rgb>
    {
        /// <inheritdoc/>
        public Rgb Convert(in LinearRgb input)
        {
            var vector = input.ToVector3();
            vector.X = input.WorkingSpace.Companding.Compress(vector.X);
            vector.Y = input.WorkingSpace.Companding.Compress(vector.Y);
            vector.Z = input.WorkingSpace.Companding.Compress(vector.Z);

            return new Rgb(vector, input.WorkingSpace);
        }
    }
}