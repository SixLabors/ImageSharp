// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation
{
    /// <summary>
    /// Color converter between Rgb and LinearRgb
    /// </summary>
    internal class RgbToLinearRgbConverter
    {
        /// <summary>
        /// Performs the conversion from the <see cref="Rgb"/> input to an instance of <see cref="LinearRgb"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public LinearRgb Convert(in Rgb input)
        {
            var vector = input.ToVector3();
            vector.X = input.WorkingSpace.Expand(vector.X);
            vector.Y = input.WorkingSpace.Expand(vector.Y);
            vector.Z = input.WorkingSpace.Expand(vector.Z);

            return new LinearRgb(vector, input.WorkingSpace);
        }
    }
}