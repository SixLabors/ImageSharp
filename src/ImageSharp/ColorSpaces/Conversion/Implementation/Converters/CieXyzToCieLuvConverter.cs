﻿// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Converts from <see cref="CieXyz"/> to <see cref="CieLuv"/>.
    /// </summary>
    internal sealed class CieXyzToCieLuvConverter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToCieLuvConverter"/> class.
        /// </summary>
        public CieXyzToCieLuvConverter()
            : this(CieLuv.DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToCieLuvConverter"/> class.
        /// </summary>
        /// <param name="luvWhitePoint">The target reference luv white point</param>
        public CieXyzToCieLuvConverter(CieXyz luvWhitePoint) => this.LuvWhitePoint = luvWhitePoint;

        /// <summary>
        /// Gets the target reference whitepoint. When not set, <see cref="CieLuv.DefaultWhitePoint"/> is used.
        /// </summary>
        public CieXyz LuvWhitePoint { get; }

        /// <summary>
        /// Performs the conversion from the <see cref="CieXyz"/> input to an instance of <see cref="CieLuv"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        public CieLuv Convert(in CieXyz input)
        {
            // Conversion algorithm described here: http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_Luv.html
            float yr = input.Y / this.LuvWhitePoint.Y;
            float up = ComputeUp(input);
            float vp = ComputeVp(input);
            float upr = ComputeUp(this.LuvWhitePoint);
            float vpr = ComputeVp(this.LuvWhitePoint);

            float l = yr > CieConstants.Epsilon ? ((116 * MathF.Pow(yr, 0.3333333F)) - 16F) : (CieConstants.Kappa * yr);

            if (float.IsNaN(l) || l < 0)
            {
                l = 0;
            }

            float u = 13 * l * (up - upr);
            float v = 13 * l * (vp - vpr);

            if (float.IsNaN(u))
            {
                u = 0;
            }

            if (float.IsNaN(v))
            {
                v = 0;
            }

            return new CieLuv(l, u, v, this.LuvWhitePoint);
        }

        /// <summary>
        /// Calculates the blue-yellow chromacity based on the given whitepoint.
        /// </summary>
        /// <param name="input">The whitepoint</param>
        /// <returns>The <see cref="float"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ComputeUp(in CieXyz input)
            => (4 * input.X) / (input.X + (15 * input.Y) + (3 * input.Z));

        /// <summary>
        /// Calculates the red-green chromacity based on the given whitepoint.
        /// </summary>
        /// <param name="input">The whitepoint</param>
        /// <returns>The <see cref="float"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static float ComputeVp(in CieXyz input)
           => (9 * input.Y) / (input.X + (15 * input.Y) + (3 * input.Z));
    }
}
