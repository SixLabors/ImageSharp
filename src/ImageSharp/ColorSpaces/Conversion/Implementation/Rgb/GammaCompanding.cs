// <copyright file="GammaCompanding.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Rgb
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Implements gamma companding
    /// </summary>
    /// <remarks>
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
    /// </remarks>
    public class GammaCompanding : ICompanding
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GammaCompanding"/> class.
        /// </summary>
        /// <param name="gamma">The gamma value.</param>
        public GammaCompanding(float gamma)
        {
            this.Gamma = gamma;
        }

        /// <summary>
        /// Gets the gamma value
        /// </summary>
        public float Gamma { get; }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Expand(float channel)
        {
            return MathF.Pow(channel, this.Gamma);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compress(float channel)
        {
            return MathF.Pow(channel, 1 / this.Gamma);
        }
    }
}