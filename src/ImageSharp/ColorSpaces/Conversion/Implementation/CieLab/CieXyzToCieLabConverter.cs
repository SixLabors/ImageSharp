// <copyright file="CieXyzToCieLabConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.CieLab
{
    using System.Runtime.CompilerServices;

    using ImageSharp.ColorSpaces;

    /// <summary>
    /// Converts from <see cref="CieXyz"/> to <see cref="CieLab"/>.
    /// </summary>
    internal class CieXyzToCieLabConverter : IColorConversion<CieXyz, CieLab>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToCieLabConverter"/> class.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyzToCieLabConverter()
            : this(CieLab.DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToCieLabConverter"/> class.
        /// </summary>
        /// <param name="labWhitePoint">The target reference lab white point</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyzToCieLabConverter(CieXyz labWhitePoint)
        {
            this.LabWhitePoint = labWhitePoint;
        }

        /// <summary>
        /// Gets the target reference whitepoint. When not set, <see cref="CieLab.DefaultWhitePoint"/> is used.
        /// </summary>
        public CieXyz LabWhitePoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLab Convert(CieXyz input)
        {
            DebugGuard.NotNull(input, nameof(input));

            // Conversion algorithm described here: http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_Lab.html
            float wx = this.LabWhitePoint.X, wy = this.LabWhitePoint.Y, wz = this.LabWhitePoint.Z;

            float xr = input.X / wx, yr = input.Y / wy, zr = input.Z / wz;

            float fx = xr > CieConstants.Epsilon ? MathF.Pow(xr, 0.3333333F) : ((CieConstants.Kappa * xr) + 16F) / 116F;
            float fy = yr > CieConstants.Epsilon ? MathF.Pow(yr, 0.3333333F) : ((CieConstants.Kappa * yr) + 16F) / 116F;
            float fz = zr > CieConstants.Epsilon ? MathF.Pow(zr, 0.3333333F) : ((CieConstants.Kappa * zr) + 16F) / 116F;

            float l = (116F * fy) - 16F;
            float a = 500F * (fx - fy);
            float b = 200F * (fy - fz);

            return new CieLab(l, a, b, this.LabWhitePoint);
        }
    }
}