// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLchuvColorSapce
{
    /// <summary>
    /// Converts from <see cref="CieLch"/> to <see cref="CieLab"/>.
    /// </summary>
    internal class CieLchuvToCieLuvConverter : IColorConversion<CieLchuv, CieLuv>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLuv Convert(in CieLchuv input)
        {
            // Conversion algorithm described here:
            // https://en.wikipedia.org/wiki/CIELUV#Cylindrical_representation_.28CIELCH.29
            float l = input.L, c = input.C, hDegrees = input.H;
            float hRadians = MathFExtensions.DegreeToRadian(hDegrees);

            float u = c * MathF.Cos(hRadians);
            float v = c * MathF.Sin(hRadians);

            return new CieLuv(l, u, v, input.WhitePoint);
        }
    }
}