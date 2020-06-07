// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Companding
{
    /// <summary>
    /// Implements Rec. 2020 companding function.
    /// </summary>
    /// <remarks>
    /// <see href="http://en.wikipedia.org/wiki/Rec._2020"/>
    /// </remarks>
    public static class Rec2020Companding
    {
        private const float Alpha = 1.09929682680944F;
        private const float AlphaMinusOne = Alpha - 1F;
        private const float Beta = 0.018053968510807F;
        private const float InverseBeta = Beta * 4.5F;
        private const float Epsilon = 1 / 0.45F;

        /// <summary>
        /// Expands a companded channel to its linear equivalent with respect to the energy.
        /// </summary>
        /// <param name="channel">The channel value.</param>
        /// <returns>The <see cref="float"/> representing the linear channel value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float Expand(float channel)
            => channel < InverseBeta ? channel / 4.5F : MathF.Pow((channel + AlphaMinusOne) / Alpha, Epsilon);

        /// <summary>
        /// Compresses an uncompanded channel (linear) to its nonlinear equivalent.
        /// </summary>
        /// <param name="channel">The channel value.</param>
        /// <returns>The <see cref="float"/> representing the nonlinear channel value.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static float Compress(float channel)
            => channel < Beta ? 4.5F * channel : (Alpha * MathF.Pow(channel, 0.45F)) - AlphaMinusOne;
    }
}