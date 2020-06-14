// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Converts from <see cref="CieLch"/> to <see cref="CieLab"/>.
    /// </summary>
    internal sealed class CieLchuvToCieLuvConverter
    {
        /// <summary>
        /// Performs the conversion from the <see cref="CieLchuv"/> input to an instance of <see cref="CieLuv"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLuv Convert(in CieLchuv input)
        {
            // Conversion algorithm described here:
            // https://en.wikipedia.org/wiki/CIELUV#Cylindrical_representation_.28CIELCH.29
            float l = input.L, c = input.C, hDegrees = input.H;
            float hRadians = GeometryUtilities.DegreeToRadian(hDegrees);

            float u = c * MathF.Cos(hRadians);
            float v = c * MathF.Sin(hRadians);

            return new CieLuv(l, u, v, input.WhitePoint);
        }
    }
}
