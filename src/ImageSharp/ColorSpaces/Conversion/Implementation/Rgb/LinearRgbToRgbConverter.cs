// <copyright file="LinearRgbToRgbConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Rgb
{
    using System.Numerics;

    using Rgb = ColorSpaces.Rgb;

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
            vector.X = input.WorkingSpace.Companding.Compress(vector.X);
            vector.Y = input.WorkingSpace.Companding.Compress(vector.Y);
            vector.Z = input.WorkingSpace.Companding.Compress(vector.Z);

            return new Rgb(vector, input.WorkingSpace);
        }
    }
}