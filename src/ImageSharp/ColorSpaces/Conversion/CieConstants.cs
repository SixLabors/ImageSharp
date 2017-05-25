// <copyright file="CieConstants.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Constants use for Cie conversion calculations
    /// <see href="http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_Lab.html"/>
    /// </summary>
    internal static class CieConstants
    {
        /// <summary>
        /// 216F / 24389F
        /// </summary>
        public const float Epsilon = 0.008856452F;

        /// <summary>
        /// 24389F / 27F
        /// </summary>
        public const float Kappa = 903.2963F;
    }
}