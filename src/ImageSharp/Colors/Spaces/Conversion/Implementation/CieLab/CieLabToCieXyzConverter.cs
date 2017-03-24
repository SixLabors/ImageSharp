// <copyright file="CieLabToCieXyzConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Conversion.Implementation
{
    using System;
    using Spaces;

    /// <summary>
    /// Converts from <see cref="CieLab"/> to <see cref="CieXyz"/>.
    /// </summary>
    public class CieLabToCieXyzConverter : IColorConversion<CieLab, CieXyz>
    {
        /// <inheritdoc/>
        public CieXyz Convert(CieLab input)
        {
            Guard.NotNull(input, nameof(input));

            // Conversion algorithm described here: http://www.brucelindbloom.com/index.html?Eqn_Lab_to_XYZ.html
            float l = input.L, a = input.A, b = input.B;
            float fy = (l + 16) / 116F;
            float fx = a / 500F + fy;
            float fz = fy - b / 200F;

            float fx3 = (float)Math.Pow(fx, 3D);
            float fz3 = (float)Math.Pow(fz, 3D);

            float xr = fx3 > CieConstants.Epsilon ? fx3 : (116F * fx - 16F) / CieConstants.Kappa;
            float yr = l > CieConstants.Kappa * CieConstants.Epsilon ? (float)Math.Pow((l + 16F) / 116F, 3D) : l / CieConstants.Kappa;
            float zr = fz3 > CieConstants.Epsilon ? fz3 : (116F * fz - 16F) / CieConstants.Kappa;

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