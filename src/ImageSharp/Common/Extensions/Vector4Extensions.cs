// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Vector4"/> struct.
    /// </summary>
    internal static class Vector4Extensions
    {
        /// <summary>
        /// Pre-multiplies the "x", "y", "z" components of a vector by its "w" component leaving the "w" component intact.
        /// </summary>
        /// <param name="source">The <see cref="Vector4"/> to premultiply</param>
        /// <returns>The <see cref="Vector4"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Premultiply(this Vector4 source)
        {
            float w = source.W;
            Vector4 premultiplied = source * w;
            premultiplied.W = w;
            return premultiplied;
        }

        /// <summary>
        /// Reverses the result of premultiplying a vector via <see cref="Premultiply(Vector4)"/>.
        /// </summary>
        /// <param name="source">The <see cref="Vector4"/> to premultiply</param>
        /// <returns>The <see cref="Vector4"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 UnPremultiply(this Vector4 source)
        {
            float w = source.W;
            Vector4 unpremultiplied = source / w;
            unpremultiplied.W = w;
            return unpremultiplied;
        }

        /// <summary>
        /// Compresses a linear color signal to its sRGB equivalent.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="linear">The <see cref="Vector4"/> whose signal to compress.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Compress(this Vector4 linear)
        {
            // TODO: Is there a faster way to do this?
            return new Vector4(Compress(linear.X), Compress(linear.Y), Compress(linear.Z), linear.W);
        }

        /// <summary>
        /// Expands an sRGB color signal to its linear equivalent.
        /// <see href="http://www.4p8.com/eric.brasseur/gamma.html#formulas"/>
        /// <see href="http://entropymine.com/imageworsener/srgbformula/"/>
        /// </summary>
        /// <param name="gamma">The <see cref="Rgba32"/> whose signal to expand.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Expand(this Vector4 gamma)
        {
            // TODO: Is there a faster way to do this?
            return new Vector4(Expand(gamma.X), Expand(gamma.Y), Expand(gamma.Z), gamma.W);
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
            if (signal <= 0.0031308F)
            {
                return signal * 12.92F;
            }

            return (1.055F * MathF.Pow(signal, 0.41666666F)) - 0.055F;
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
            if (signal <= 0.04045F)
            {
                return signal / 12.92F;
            }

            return MathF.Pow((signal + 0.055F) / 1.055F, 2.4F);
        }
    }
}