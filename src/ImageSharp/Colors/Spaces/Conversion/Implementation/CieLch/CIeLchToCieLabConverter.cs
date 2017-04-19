﻿// <copyright file="CieLchToCieLabConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion.Implementation.CieLch
{
    using ImageSharp.Colors.Spaces;

    /// <summary>
    /// Converts from <see cref="CieLch"/> to <see cref="CieLab"/>.
    /// </summary>
    public class CieLchToCieLabConverter : IColorConversion<CieLch, CieLab>
    {
        /// <inheritdoc/>
        public CieLab Convert(CieLch input)
        {
            DebugGuard.NotNull(input, nameof(input));

            // Conversion algorithm described here: https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC
            float l = input.L, c = input.C, hDegrees = input.H;
            float hRadians = MathF.DegreeToRadian(hDegrees);

            float a = c * MathF.Cos(hRadians);
            float b = c * MathF.Sin(hRadians);

            return new CieLab(l, a, b, input.WhitePoint);
        }
    }
}