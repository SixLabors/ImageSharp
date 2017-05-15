// <copyright file="CieLchuvToCieLuvConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.CieLchuv
{
    using System.Runtime.CompilerServices;

    using ImageSharp.ColorSpaces;

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
            float hRadians = MathF.DegreeToRadian(hDegrees);

            float u = c * MathF.Cos(hRadians);
            float v = c * MathF.Sin(hRadians);

            return new CieLuv(l, u, v, input.WhitePoint);
        }
    }
}