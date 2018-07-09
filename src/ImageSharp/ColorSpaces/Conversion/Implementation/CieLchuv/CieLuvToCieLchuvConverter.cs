// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLchuvColorSapce
{
    /// <summary>
    /// Converts from <see cref="CieLab"/> to <see cref="CieLch"/>.
    /// </summary>
    internal class CieLuvToCieLchuvConverter : IColorConversion<CieLuv, CieLchuv>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLchuv Convert(in CieLuv input)
        {
            // Conversion algorithm described here:
            // https://en.wikipedia.org/wiki/CIELUV#Cylindrical_representation_.28CIELCH.29
            float l = input.L, a = input.U, b = input.V;
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

            return new CieLchuv(l, c, hDegrees, input.WhitePoint);
        }
    }
}