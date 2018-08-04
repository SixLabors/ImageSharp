// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLchColorSapce
{
    /// <summary>
    /// Converts from <see cref="CieLab"/> to <see cref="CieLch"/>.
    /// </summary>
    internal class CieLabToCieLchConverter : IColorConversion<CieLab, CieLch>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLch Convert(in CieLab input)
        {
            // Conversion algorithm described here:
            // https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC
            float l = input.L, a = input.A, b = input.B;
            float c = MathF.Sqrt((a * a) + (b * b));
            float hRadians = MathF.Atan2(b, a);
            float hDegrees = MathFExtensions.RadianToDegree(hRadians);

            // Wrap the angle round at 360.
            hDegrees = hDegrees % 360;

            // Make sure it's not negative.
            while (hDegrees < 0)
            {
                hDegrees += 360;
            }

            return new CieLch(l, c, hDegrees, input.WhitePoint);
        }
    }
}