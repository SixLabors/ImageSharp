// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Companding
{
    /// <summary>
    /// Implements Rec. 2020 companding function (for 12-bits).
    /// </summary>
    /// <remarks>
    /// <see href="http://en.wikipedia.org/wiki/Rec._2020"/>
    /// For 10-bits, companding is identical to <see cref="Rec709Companding"/>
    /// </remarks>
    public static class Rec2020Companding
    {
        /// <summary>
        /// Expands a companded channel to its linear equivalent with respect to the energy.
        /// </summary>
        /// <param name="channel">The channel value.</param>
        /// <returns>The <see cref="float"/> representing the linear channel value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float Expand(float channel)
            => channel < 0.08145F ? channel / 4.5F : MathF.Pow((channel + 0.0993F) / 1.0993F, 2.222222F);

        /// <summary>
        /// Compresses an uncompanded channel (linear) to its nonlinear equivalent.
        /// </summary>
        /// <param name="channel">The channel value.</param>
        /// <returns>The <see cref="float"/> representing the nonlinear channel value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float Compress(float channel)
            => channel < 0.0181F ? 4500F * channel : (1.0993F * channel) - 0.0993F;
    }
}