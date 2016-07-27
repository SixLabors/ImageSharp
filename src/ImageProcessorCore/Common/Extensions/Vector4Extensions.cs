// <copyright file="Vector4Extensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Extension methods for the <see cref="Vector4"/> struct.
    /// </summary>
    public static class Vector4Extensions
    {
        /// <summary>
        /// Compresses a linear color signal to its sRGB equivalent.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="linear">The <see cref="Vector4"/> whose signal to compress.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        public static Vector4 Compress(this Vector4 linear)
        {
            // TODO: Is there a faster way to do this?
            float r = Compress(linear.X);
            float g = Compress(linear.Y);
            float b = Compress(linear.Z);

            return new Vector4(r, g, b, linear.W);
        }

        /// <summary>
        /// Expands an sRGB color signal to its linear equivalent.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="gamma">The <see cref="Color"/> whose signal to expand.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        public static Vector4 Expand(this Vector4 gamma)
        {
            // TODO: Is there a faster way to do this?
            float r = Expand(gamma.X);
            float g = Expand(gamma.Y);
            float b = Expand(gamma.Z);

            return new Vector4(r, g, b, gamma.W);
        }

        /// <summary>
        /// Gets the compressed sRGB value from an linear signal.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="signal">The signal value to compress.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Compress(float signal)
        {
            if (signal <= 0.0031308f)
            {
                return signal * 12.92f;
            }

            return (1.055f * (float)Math.Pow(signal, 0.41666666f)) - 0.055f;
        }

        /// <summary>
        /// Gets the expanded linear value from an sRGB signal.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="signal">The signal value to expand.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Expand(float signal)
        {
            if (signal <= 0.04045f)
            {
                return signal / 12.92f;
            }

            return (float)Math.Pow((signal + 0.055f) / 1.055f, 2.4f);
        }
    }
}
