// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation
{
    /// <summary>
    /// Implements L* companding
    /// </summary>
    /// <remarks>
    /// For more info see:
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
    /// </remarks>
    public sealed class LCompanding : ICompanding
    {
        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public float Expand(float channel)
            => channel <= 0.08 ? 100 * channel / CieConstants.Kappa : ImageMaths.Pow3((channel + 0.16F) / 1.16F);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public float Compress(float channel)
        {
            return channel <= CieConstants.Epsilon
                ? channel * CieConstants.Kappa / 100F
                : MathF.Pow(1.16F * channel, 0.3333333F) - 0.16F;
        }
    }
}