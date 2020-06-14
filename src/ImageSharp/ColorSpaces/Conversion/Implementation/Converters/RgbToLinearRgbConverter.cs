// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Color converter between Rgb and LinearRgb.
    /// </summary>
    internal class RgbToLinearRgbConverter
    {
        /// <summary>
        /// Performs the conversion from the <see cref="Rgb"/> input to an instance of <see cref="LinearRgb"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public LinearRgb Convert(in Rgb input)
        {
            return new LinearRgb(
                r: input.WorkingSpace.Expand(input.R),
                g: input.WorkingSpace.Expand(input.G),
                b: input.WorkingSpace.Expand(input.B),
                workingSpace: input.WorkingSpace);
        }
    }
}
