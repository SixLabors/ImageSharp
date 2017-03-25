// <copyright file="ICompanding.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces
{
    /// <summary>
    /// Pair of companding functions for <see cref="IRgbWorkingSpace"/>.
    /// Used for conversion to <see cref="CieXyz"/> and backwards.
    /// See also: <seealso cref="IRgbWorkingSpace.Companding"/>
    /// </summary>
    public interface ICompanding
    {
        /// <summary>
        /// Companded channel is made linear with respect to the energy.
        /// </summary>
        /// <remarks>
        /// For more info see:
        /// http://www.brucelindbloom.com/index.html?Eqn_RGB_to_XYZ.html
        /// </remarks>
        /// <param name="channel">The channel value</param>
        /// <returns>The linear channel value</returns>
        float InverseCompanding(float channel);

        /// <summary>
        /// Uncompanded channel (linear) is made nonlinear (depends on the RGB color system).
        /// </summary>
        /// <remarks>
        /// For more info see:
        /// http://www.brucelindbloom.com/index.html?Eqn_XYZ_to_RGB.html
        /// </remarks>
        /// <param name="channel">The channel value</param>
        /// <returns>The nonlinear channel value</returns>
        float Companding(float channel);
    }
}
