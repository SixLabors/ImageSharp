// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// Extension methods for <see cref="Size"/>
    /// </summary>
    internal static class SizeExtensions
    {
        /// <summary>
        /// Multiplies 'a.Width' with 'b.Width' and 'a.Height' with 'b.Height'.
        /// TODO: Shouldn't we expose this as operator in SixLabors.Core?
        /// </summary>
        public static Size MultiplyBy(this Size a, Size b) => new Size(a.Width * b.Width, a.Height * b.Height);

        /// <summary>
        /// Divides 'a.Width' with 'b.Width' and 'a.Height' with 'b.Height'.
        /// TODO: Shouldn't we expose this as operator in SixLabors.Core?
        /// </summary>
        public static Size DivideBy(this Size a, Size b) => new Size(a.Width / b.Width, a.Height / b.Height);

        /// <summary>
        /// Divide Width and Height as real numbers and return the Ceiling.
        /// </summary>
        public static Size DivideRoundUp(this Size originalSize, int divX, int divY)
        {
            var sizeVect = (Vector2)(SizeF)originalSize;
            sizeVect /= new Vector2(divX, divY);
            sizeVect.X = MathF.Ceiling(sizeVect.X);
            sizeVect.Y = MathF.Ceiling(sizeVect.Y);

            return new Size((int)sizeVect.X, (int)sizeVect.Y);
        }

        /// <summary>
        /// Divide Width and Height as real numbers and return the Ceiling.
        /// </summary>
        public static Size DivideRoundUp(this Size originalSize, int divisor) =>
            DivideRoundUp(originalSize, divisor, divisor);

        /// <summary>
        /// Divide Width and Height as real numbers and return the Ceiling.
        /// </summary>
        public static Size DivideRoundUp(this Size originalSize, Size divisor) =>
            DivideRoundUp(originalSize, divisor.Width, divisor.Height);
    }
}