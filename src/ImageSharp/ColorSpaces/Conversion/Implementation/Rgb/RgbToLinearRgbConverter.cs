// <copyright file="RgbToLinearRgbConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Rgb
{
    using System.Numerics;

    using Rgb = ColorSpaces.Rgb;

    /// <summary>
    /// Color converter between Rgb and LinearRgb
    /// </summary>
    internal class RgbToLinearRgbConverter : IColorConversion<Rgb, LinearRgb>
    {
        /// <inheritdoc/>
        public LinearRgb Convert(Rgb input)
        {
            Guard.NotNull(input, nameof(input));

            Vector3 vector = input.Vector;
            vector.X = input.WorkingSpace.Companding.Expand(vector.X);
            vector.Y = input.WorkingSpace.Companding.Expand(vector.Y);
            vector.Z = input.WorkingSpace.Companding.Expand(vector.Z);

            return new LinearRgb(vector, input.WorkingSpace);
        }
    }
}