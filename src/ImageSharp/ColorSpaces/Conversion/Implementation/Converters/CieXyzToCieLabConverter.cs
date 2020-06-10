// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Converts from <see cref="CieXyz"/> to <see cref="CieLab"/>.
    /// </summary>
    internal sealed class CieXyzToCieLabConverter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToCieLabConverter"/> class.
        /// </summary>
        public CieXyzToCieLabConverter()
            : this(CieLab.DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToCieLabConverter"/> class.
        /// </summary>
        /// <param name="labWhitePoint">The target reference lab white point</param>
        public CieXyzToCieLabConverter(CieXyz labWhitePoint) => this.LabWhitePoint = labWhitePoint;

        /// <summary>
        /// Gets the target reference whitepoint. When not set, <see cref="CieLab.DefaultWhitePoint"/> is used.
        /// </summary>
        public CieXyz LabWhitePoint { get; }

        /// <summary>
        /// Performs the conversion from the <see cref="CieXyz"/> input to an instance of <see cref="CieLab"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLab Convert(in CieXyz input)
        {
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