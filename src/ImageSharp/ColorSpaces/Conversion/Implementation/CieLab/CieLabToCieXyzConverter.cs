// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLabColorSapce
{
    /// <summary>
    /// Converts from <see cref="CieLab"/> to <see cref="CieXyz"/>.
    /// </summary>
    internal class CieLabToCieXyzConverter : IColorConversion<CieLab, CieXyz>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyz Convert(in CieLab input)
        {
            // Conversion algorithm described here: http://www.brucelindbloom.com/index.html?Eqn_Lab_to_XYZ.html
            float l = input.L, a = input.A, b = input.B;
            float fy = (l + 16) / 116F;
            float fx = (a / 500F) + fy;
            float fz = fy - (b / 200F);

            float fx3 = MathF.Pow(fx, 3F);
            float fz3 = MathF.Pow(fz, 3F);

            float xr = fx3 > CieConstants.Epsilon ? fx3 : ((116F * fx) - 16F) / CieConstants.Kappa;
            float yr = l > CieConstants.Kappa * CieConstants.Epsilon ? MathF.Pow((l + 16F) / 116F, 3F) : l / CieConstants.Kappa;
            float zr = fz3 > CieConstants.Epsilon ? fz3 : ((116F * fz) - 16F) / CieConstants.Kappa;

            float wx = input.WhitePoint.X, wy = input.WhitePoint.Y, wz = input.WhitePoint.Z;

            // Avoids XYZ coordinates out range (restricted by 0 and XYZ reference white)
            xr = xr.Clamp(0, 1F);
            yr = yr.Clamp(0, 1F);
            zr = zr.Clamp(0, 1F);

            float x = xr * wx;
            float y = yr * wy;
            float z = zr * wz;

            return new CieXyz(x, y, z);
        }
    }
}