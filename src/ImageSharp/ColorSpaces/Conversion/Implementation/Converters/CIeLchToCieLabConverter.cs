// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Converts from <see cref="CieLch"/> to <see cref="CieLab"/>.
    /// </summary>
    internal sealed class CieLchToCieLabConverter
    {
        /// <summary>
        /// Performs the conversion from the <see cref="CieLch"/> input to an instance of <see cref="CieLab"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLab Convert(in CieLch input)
        {
            // Conversion algorithm described here:
            // https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC
            float l = input.L, c = input.C, hDegrees = input.H;
            float hRadians = GeometryUtilities.DegreeToRadian(hDegrees);

            float a = c * MathF.Cos(hRadians);
            float b = c * MathF.Sin(hRadians);

            return new CieLab(l, a, b, input.WhitePoint);
        }
    }
}