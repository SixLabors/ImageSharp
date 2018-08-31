// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLchColorSapce
{
    /// <summary>
    /// Converts from <see cref="CieLch"/> to <see cref="CieLab"/>.
    /// </summary>
    internal class CieLchToCieLabConverter : IColorConversion<CieLch, CieLab>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLab Convert(in CieLch input)
        {
            // Conversion algorithm described here:
            // https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC
            float l = input.L, c = input.C, hDegrees = input.H;
            float hRadians = MathFExtensions.DegreeToRadian(hDegrees);

            float a = c * MathF.Cos(hRadians);
            float b = c * MathF.Sin(hRadians);

            return new CieLab(l, a, b, input.WhitePoint);
        }
    }
}