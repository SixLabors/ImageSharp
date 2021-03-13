// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Converts from <see cref="CieLuv"/> to <see cref="CieXyz"/>.
    /// </summary>
    internal sealed class CieLuvToCieXyzConverter
    {
        /// <summary>
        /// Performs the conversion from the <see cref="CieLuv"/> input to an instance of <see cref="CieXyz"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        public CieXyz Convert(in CieLuv input)
        {
            // Conversion algorithm described here: http://www.brucelindbloom.com/index.html?Eqn_Luv_to_XYZ.html
            float l = input.L, u = input.U, v = input.V;

            float u0 = ComputeU0(input.WhitePoint);
            float v0 = ComputeV0(input.WhitePoint);

            float y = l > CieConstants.Kappa * CieConstants.Epsilon
                        ? Numerics.Pow3((l + 16) / 116)
                        : l / CieConstants.Kappa;

            float a = ((52 * l / (u + (13 * l * u0))) - 1) / 3;
            float b = -5 * y;
            const float c = -0.3333333F;
            float d = y * ((39 * l / (v + (13 * l * v0))) - 5);

            float x = (d - b) / (a - c);
            float z = (x * a) + b;

            if (float.IsNaN(x) || x < 0)
            {
                x = 0;
            }

            if (float.IsNaN(y) || y < 0)
            {
                y = 0;
            }

            if (float.IsNaN(z) || z < 0)
            {
                z = 0;
            }

            return new CieXyz(x, y, z);
        }

        /// <summary>
        /// Calculates the blue-yellow chromacity based on the given whitepoint.
        /// </summary>
        /// <param name="input">The whitepoint</param>
        /// <returns>The <see cref="float"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static float ComputeU0(in CieXyz input)
            => (4 * input.X) / (input.X + (15 * input.Y) + (3 * input.Z));

        /// <summary>
        /// Calculates the red-green chromacity based on the given whitepoint.
        /// </summary>
        /// <param name="input">The whitepoint</param>
        /// <returns>The <see cref="float"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static float ComputeV0(in CieXyz input)
            => (9 * input.Y) / (input.X + (15 * input.Y) + (3 * input.Z));
    }
}
