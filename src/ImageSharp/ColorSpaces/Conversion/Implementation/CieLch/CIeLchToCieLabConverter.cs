// <copyright file="CieLchToCieLabConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.CieLch
{
    using System.Runtime.CompilerServices;

    using ImageSharp.ColorSpaces;

    /// <summary>
    /// Converts from <see cref="CieLch"/> to <see cref="CieLab"/>.
    /// </summary>
    internal class CieLchToCieLabConverter : IColorConversion<CieLch, CieLab>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLab Convert(CieLch input)
        {
            DebugGuard.NotNull(input, nameof(input));

            // Conversion algorithm described here:
            // https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC
            float l = input.L, c = input.C, hDegrees = input.H;
            float hRadians = MathF.DegreeToRadian(hDegrees);

            float a = c * MathF.Cos(hRadians);
            float b = c * MathF.Sin(hRadians);

            return new CieLab(l, a, b, input.WhitePoint);
        }
    }
}