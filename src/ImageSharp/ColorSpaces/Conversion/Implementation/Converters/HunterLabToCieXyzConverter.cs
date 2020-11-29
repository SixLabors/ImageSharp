// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Color converter between <see cref="HunterLab"/> and <see cref="CieXyz"/>
    /// </summary>
    internal sealed class HunterLabToCieXyzConverter : CieXyzAndHunterLabConverterBase
    {
        /// <summary>
        /// Performs the conversion from the <see cref="HunterLab"/> input to an instance of <see cref="CieXyz"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieXyz Convert(in HunterLab input)
        {
            // Conversion algorithm described here: http://en.wikipedia.org/wiki/Lab_color_space#Hunter_Lab
            float l = input.L, a = input.A, b = input.B;
            float xn = input.WhitePoint.X, yn = input.WhitePoint.Y, zn = input.WhitePoint.Z;

            float ka = ComputeKa(input.WhitePoint);
            float kb = ComputeKb(input.WhitePoint);

            float pow = Numerics.Pow2(l / 100F);
            float sqrtPow = MathF.Sqrt(pow);
            float y = pow * yn;

            float x = (((a / ka) * sqrtPow) + pow) * xn;
            float z = (((b / kb) * sqrtPow) - pow) * (-zn);

            return new CieXyz(x, y, z);
        }
    }
}
