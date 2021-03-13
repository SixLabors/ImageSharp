// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Converts from <see cref="CieLab"/> to <see cref="CieXyz"/>.
    /// </summary>
    internal sealed class CieLabToCieXyzConverter
    {
        /// <summary>
        /// Performs the conversion from the <see cref="CieLab"/> input to an instance of <see cref="CieXyz"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieXyz Convert(in CieLab input)
        {
            // Conversion algorithm described here: http://www.brucelindbloom.com/index.html?Eqn_Lab_to_XYZ.html
            float l = input.L, a = input.A, b = input.B;
            float fy = (l + 16) / 116F;
            float fx = (a / 500F) + fy;
            float fz = fy - (b / 200F);

            float fx3 = Numerics.Pow3(fx);
            float fz3 = Numerics.Pow3(fz);

            float xr = fx3 > CieConstants.Epsilon ? fx3 : ((116F * fx) - 16F) / CieConstants.Kappa;
            float yr = l > CieConstants.Kappa * CieConstants.Epsilon ? Numerics.Pow3((l + 16F) / 116F) : l / CieConstants.Kappa;
            float zr = fz3 > CieConstants.Epsilon ? fz3 : ((116F * fz) - 16F) / CieConstants.Kappa;

            var wxyz = new Vector3(input.WhitePoint.X, input.WhitePoint.Y, input.WhitePoint.Z);

            // Avoids XYZ coordinates out range (restricted by 0 and XYZ reference white)
            var xyzr = Vector3.Clamp(new Vector3(xr, yr, zr), Vector3.Zero, Vector3.One);

            Vector3 xyz = xyzr * wxyz;
            return new CieXyz(xyz);
        }
    }
}
