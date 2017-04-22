// <copyright file="Rec2020Companding.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.Rgb
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Implements the Rec. 709 companding function
    /// </summary>
    /// <remarks>
    /// http://en.wikipedia.org/wiki/Rec._709
    /// </remarks>
    public class Rec709Companding : ICompanding
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Expand(float channel)
        {
            return channel < 0.081F ? channel / 4.5F : MathF.Pow((channel + 0.099F) / 1.099F, 2.222222F);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Compress(float channel)
        {
            return channel < 0.018F ? 4500F * channel : (1.099F * channel) - 0.099F;
        }
    }
}