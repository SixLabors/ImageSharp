// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.HunterLabColorSapce
{
    /// <summary>
    /// Color converter between HunterLab and CieXyz
    /// </summary>
    internal class HunterLabToCieXyzConverter : CieXyzAndHunterLabConverterBase, IColorConversion<HunterLab, CieXyz>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyz Convert(in HunterLab input)
        {
            // Conversion algorithm described here: http://en.wikipedia.org/wiki/Lab_color_space#Hunter_Lab
            float l = input.L, a = input.A, b = input.B;
            float xn = input.WhitePoint.X, yn = input.WhitePoint.Y, zn = input.WhitePoint.Z;

            float ka = ComputeKa(input.WhitePoint);
            float kb = ComputeKb(input.WhitePoint);

            float y = MathF.Pow(l / 100F, 2) * yn;
            float x = (((a / ka) * MathF.Sqrt(y / yn)) + (y / yn)) * xn;
            float z = (((b / kb) * MathF.Sqrt(y / yn)) - (y / yn)) * (-zn);

            return new CieXyz(x, y, z);
        }
    }
}