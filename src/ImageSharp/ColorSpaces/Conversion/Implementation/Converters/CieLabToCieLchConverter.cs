// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Converts from <see cref="CieLab"/> to <see cref="CieLch"/>.
    /// </summary>
    internal sealed class CieLabToCieLchConverter
    {
        /// <summary>
        /// Performs the conversion from the <see cref="CieLab"/> input to an instance of <see cref="CieLch"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLch Convert(in CieLab input)
        {
            // Conversion algorithm described here:
            // https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC
            float l = input.L, a = input.A, b = input.B;
            float c = MathF.Sqrt((a * a) + (b * b));
            float hRadians = MathF.Atan2(b, a);
            float hDegrees = GeometryUtilities.RadianToDegree(hRadians);

            // Wrap the angle round at 360.
            hDegrees %= 360;

            // Make sure it's not negative.
            while (hDegrees < 0)
            {
                hDegrees += 360;
            }

            return new CieLch(l, c, hDegrees, input.WhitePoint);
        }
    }
}
