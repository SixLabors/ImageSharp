// <copyright file="Rec2020Companding.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Rgb
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Implements Rec. 2020 companding function (for 12-bits).
    /// </summary>
    /// <remarks>
    /// <see href="http://en.wikipedia.org/wiki/Rec._2020"/>
    /// For 10-bits, companding is identical to <see cref="Rec709Companding"/>
    /// </remarks>
    public class Rec2020Companding : ICompanding
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Expand(float channel)
        {
            return channel < 0.08145F ? channel / 4.5F : MathF.Pow((channel + 0.0993F) / 1.0993F, 2.222222F);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compress(float channel)
        {
            return channel < 0.0181F ? 4500F * channel : (1.0993F * channel) - 0.0993F;
        }
    }
}