// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        /// Bulk variant of <see cref="Premultiply(System.Numerics.Vector4)"/>
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        public static void Premultiply(Span<Vector4> vectors)
        {
            // TODO: This method can be AVX2 optimized using Vector<float>
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(vectors);

            for (int i = 0; i < vectors.Length; i++)
            {
                ref Vector4 v = ref Unsafe.Add(ref baseRef, i);
                var s = new Vector4(v.W);
                s.W = 1;
                v *= s;
            }
        }

        /// <summary>
        /// Bulk variant of <see cref="UnPremultiply(System.Numerics.Vector4)"/>
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        public static void UnPremultiply(Span<Vector4> vectors)
        {
            // TODO: This method can be AVX2 optimized using Vector<float>
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(vectors);

            for (int i = 0; i < vectors.Length; i++)
            {
                ref Vector4 v = ref Unsafe.Add(ref baseRef, i);
                var s = new Vector4(1 / v.W);
                s.W = 1;
                v *= s;
            }
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
        /// Bulk variant of <see cref="Compress(System.Numerics.Vector4)"/>
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        public static void Compress(Span<Vector4> vectors)
        {
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(vectors);

            for (int i = 0; i < vectors.Length; i++)
            {
                ref Vector4 v = ref Unsafe.Add(ref baseRef, i);
                v.X = Compress(v.X);
                v.Y = Compress(v.Y);
                v.Z = Compress(v.Z);
            }
        }

        /// <summary>
        /// Bulk variant of <see cref="Expand(System.Numerics.Vector4)"/>
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        public static void Expand(Span<Vector4> vectors)
        {
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(vectors);

            for (int i = 0; i < vectors.Length; i++)
            {
                ref Vector4 v = ref Unsafe.Add(ref baseRef, i);
                v.X = Expand(v.X);
                v.Y = Expand(v.Y);
                v.Z = Expand(v.Z);
            }
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