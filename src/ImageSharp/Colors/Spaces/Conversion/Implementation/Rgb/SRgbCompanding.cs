// <copyright file="SRgbCompanding.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion.Implementation.Rgb
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Implements sRGB companding
    /// </summary>
    /// <remarks>
    /// For more info see:
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html"/>
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html"/>
    /// </remarks>
    public class SRgbCompanding : ICompanding
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Expand(float channel)
        {
            return channel <= 0.04045F ? channel / 12.92F : (float)Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compress(float channel)
        {
            return channel <= 0.0031308F ? 12.92F * channel : (1.055F * (float)Math.Pow(channel, 0.416666666666667D)) - 0.055F;
        }
    }
}