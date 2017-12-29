// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorSpaces;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLchuvColorSapce
{
    /// <summary>
    /// Converts from <see cref="CieLch"/> to <see cref="CieLab"/>.
    /// </summary>
    internal class CieLchuvToCieLuvConverter : IColorConversion<CieLchuv, CieLuv>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLuv Convert(CieLchuv input)
        {
            DebugGuard.NotNull(input, nameof(input));

            // Conversion algorithm described here:
            // https://en.wikipedia.org/wiki/CIELUV#Cylindrical_representation_.28CIELCH.29
            float l = input.L, c = input.C, hDegrees = input.H;
            float hRadians = MathFExtensions.DegreeToRadian(hDegrees);

            float u = c * (float)Math.Cos(hRadians);
            float v = c * (float)Math.Sin(hRadians);

            return new CieLuv(l, u, v, input.WhitePoint);
        }
    }
}