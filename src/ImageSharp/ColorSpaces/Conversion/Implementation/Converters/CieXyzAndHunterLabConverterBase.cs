// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
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
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float ComputeKa(CieXyz whitePoint)
        {
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
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float ComputeKb(CieXyz whitePoint)
        {
            if (whitePoint == Illuminants.C)
            {
                return 70F;
            }

            return 100F * (70F / 218.11F) * (whitePoint.Y + whitePoint.Z);
        }
    }
}