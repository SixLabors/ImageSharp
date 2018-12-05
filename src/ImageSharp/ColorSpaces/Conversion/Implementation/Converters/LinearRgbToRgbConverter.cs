// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation
{
    /// <summary>
    /// Color converter between <see cref="LinearRgb"/> and <see cref="Rgb"/>
    /// </summary>
    internal sealed class LinearRgbToRgbConverter
    {
        /// <summary>
        /// Performs the conversion from the <see cref="LinearRgb"/> input to an instance of <see cref="Rgb"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgb Convert(in LinearRgb input)
        {
            var vector = input.ToVector3();
            vector.X = input.WorkingSpace.Compress(vector.X);
            vector.Y = input.WorkingSpace.Compress(vector.Y);
            vector.Z = input.WorkingSpace.Compress(vector.Z);

            return new Rgb(vector, input.WorkingSpace);
        }
    }
}