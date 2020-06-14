// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Color converter between <see cref="LinearRgb"/> and <see cref="Rgb"/>.
    /// </summary>
    internal sealed class LinearRgbToRgbConverter
    {
        /// <summary>
        /// Performs the conversion from the <see cref="LinearRgb"/> input to an instance of <see cref="Rgb"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgb Convert(in LinearRgb input)
        {
            return new Rgb(
                r: input.WorkingSpace.Compress(input.R),
                g: input.WorkingSpace.Compress(input.G),
                b: input.WorkingSpace.Compress(input.B),
                workingSpace: input.WorkingSpace);
        }
    }
}
