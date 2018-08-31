// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieLuvColorSapce
{
    /// <summary>
    /// Converts from <see cref="CieLuv"/> to <see cref="CieXyz"/>.
    /// </summary>
    internal class CieLuvToCieXyzConverter : IColorConversion<CieLuv, CieXyz>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyz Convert(in CieLuv input)
        {
            // Conversion algorithm described here: http://www.brucelindbloom.com/index.html?Eqn_Luv_to_XYZ.html
            float l = input.L, u = input.U, v = input.V;

            float u0 = ComputeU0(input.WhitePoint);
            float v0 = ComputeV0(input.WhitePoint);

            float y = l > CieConstants.Kappa * CieConstants.Epsilon
                        ? MathF.Pow((l + 16) / 116, 3)
                        : l / CieConstants.Kappa;

            float a = ((52 * l / (u + (13 * l * u0))) - 1) / 3;
            float b = -5 * y;
            float c = -0.3333333F;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ComputeU0(in CieXyz input)
        {
            return (4 * input.X) / (input.X + (15 * input.Y) + (3 * input.Z));
        }

        /// <summary>
        /// Calculates the red-green chromacity based on the given whitepoint.
        /// </summary>
        /// <param name="input">The whitepoint</param>
        /// <returns>The <see cref="float"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ComputeV0(in CieXyz input)
        {
            return (9 * input.Y) / (input.X + (15 * input.Y) + (3 * input.Z));
        }
    }
}