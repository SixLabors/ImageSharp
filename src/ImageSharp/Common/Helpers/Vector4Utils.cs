// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Utility methods for the <see cref="Vector4"/> struct.
    /// </summary>
    internal static class Vector4Utils
    {
        /// <summary>
        /// Pre-multiplies the "x", "y", "z" components of a vector by its "w" component leaving the "w" component intact.
        /// </summary>
        /// <param name="source">The <see cref="Vector4"/> to premultiply</param>
        /// <returns>The <see cref="Vector4"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Vector4 Premultiply(Vector4 source)
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
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Vector4 UnPremultiply(Vector4 source)
        {
            float w = source.W;
            Vector4 unpremultiplied = source / w;
            unpremultiplied.W = w;
            return unpremultiplied;
        }

        /// <summary>
        /// Bulk variant of <see cref="Premultiply(Vector4)"/>
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Premultiply(Span<Vector4> vectors)
        {
            // TODO: This method can be AVX2 optimized using Vector<float>
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(vectors);

            for (int i = 0; i < vectors.Length; i++)
            {
                ref Vector4 v = ref Unsafe.Add(ref baseRef, i);
                var s = new Vector4(v.W)
                {
                    W = 1
                };
                v *= s;
            }
        }

        /// <summary>
        /// Bulk variant of <see cref="UnPremultiply(Vector4)"/>
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnPremultiply(Span<Vector4> vectors)
        {
            // TODO: This method can be AVX2 optimized using Vector<float>
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(vectors);

            for (int i = 0; i < vectors.Length; i++)
            {
                ref Vector4 v = ref Unsafe.Add(ref baseRef, i);
                var s = new Vector4(1 / v.W)
                {
                    W = 1
                };
                v *= s;
            }
        }
    }
}