// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation
{
    /// <summary>
    /// Color converter between CMYK and Rgb
    /// </summary>
    internal class CmykAndRgbConverter : IColorConversion<Cmyk, Rgb>, IColorConversion<Rgb, Cmyk>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgb Convert(Cmyk input)
        {
            Vector3 rgb = (Vector3.One - new Vector3(input.C, input.M, input.Y)) * (Vector3.One - new Vector3(input.K));
            return new Rgb(rgb);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Cmyk Convert(Rgb input)
        {
            // To CMYK
            Vector3 cmy = Vector3.One - input.Vector;

            // To CMYK
            var k = new Vector3(MathF.Min(cmy.X, MathF.Min(cmy.Y, cmy.Z)));

            if (MathF.Abs(k.X - 1F) < Constants.Epsilon)
            {
                return new Cmyk(0, 0, 0, 1F);
            }

            cmy = (cmy - k) / (Vector3.One - k);

            return new Cmyk(cmy.X, cmy.Y, cmy.Z, k.X);
        }
    }
}