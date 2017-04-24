// <copyright file="CieXyzAndHunterLabConverterBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.HunterLab
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The base class for converting between <see cref="HunterLab"/> and <see cref="CieXyz"/> color spaces.
    /// </summary>
    internal abstract class CieXyzAndHunterLabConverterBase
    {
        /// <summary>
        /// Returns the Ka coefficient that depends upon the whitepoint illuminant.
        /// </summary>
        /// <param name="whitePoint">The whitepoint</param>
        /// <returns>The <see cref="float"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeKa(CieXyz whitePoint)
        {
            DebugGuard.NotNull(whitePoint, nameof(whitePoint));

            if (whitePoint.Equals(Illuminants.C))
            {
                return 175F;
            }

            return 100F * (175F / 198.04F) * (whitePoint.X + whitePoint.Y);
        }

        /// <summary>
        /// Returns the Kb coefficient that depends upon the whitepoint illuminant.
        /// </summary>
        /// <param name="whitePoint">The whitepoint</param>
        /// <returns>The <see cref="float"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeKb(CieXyz whitePoint)
        {
            DebugGuard.NotNull(whitePoint, nameof(whitePoint));

            if (whitePoint == Illuminants.C)
            {
                return 70F;
            }

            return 100F * (70F / 218.11F) * (whitePoint.Y + whitePoint.Z);
        }
    }
}