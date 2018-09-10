// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats.PixelBlenders
{
    /// <summary>
    /// Collection of Porter Duff Color Blending and Alpha Composition Functions.
    /// </summary>
    /// <remarks>
    /// These functions are designed to be a general solution for all color cases,
    /// that is, they take in account the alpha value of both the backdrop
    /// and source, and there's no need to alpha-premultiply neither the backdrop
    /// nor the source.
    /// Note there are faster functions for when the backdrop color is known
    /// to be opaque
    /// </remarks>
    internal static partial class PorterDuffFunctions
    {
        /// <summary>
        /// Source over backdrop
        /// </summary>
        /// <param name="backdrop">Backdrop color</param>
        /// <param name="source">Source color</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Normal(Vector4 backdrop, Vector4 source)
        {
            return source;
        }

        /// <summary>
        /// Source multiplied by backdrop
        /// </summary>
        /// <param name="backdrop">Backdrop color</param>
        /// <param name="source">Source color</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Multiply(Vector4 backdrop, Vector4 source)
        {
            return backdrop * source;
        }

        /// <summary>
        /// Source added to backdrop
        /// </summary>
        /// <param name="backdrop">Backdrop color</param>
        /// <param name="source">Source color</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Add(Vector4 backdrop, Vector4 source)
        {
            return Vector4.Min(Vector4.One, backdrop + source);
        }

        /// <summary>
        /// Source subtracted from backdrop
        /// </summary>
        /// <param name="backdrop">Backdrop color</param>
        /// <param name="source">Source color</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Subtract(Vector4 backdrop, Vector4 source)
        {
            return Vector4.Max(Vector4.Zero, backdrop - source);
        }

        /// <summary>
        /// Complement of source multiplied by the complement of backdrop
        /// </summary>
        /// <param name="backdrop">Backdrop color</param>
        /// <param name="source">Source color</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Screen(Vector4 backdrop, Vector4 source)
        {
            return Vector4.One - ((Vector4.One - backdrop) * (Vector4.One - source));
        }

        /// <summary>
        /// Per element, chooses the smallest value of source and backdrop
        /// </summary>
        /// <param name="backdrop">Backdrop color</param>
        /// <param name="source">Source color</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Darken(Vector4 backdrop, Vector4 source)
        {
            return Vector4.Min(backdrop, source);
        }

        /// <summary>
        /// Per element, chooses the largest value of source and backdrop
        /// </summary>
        /// <param name="backdrop">Backdrop color</param>
        /// <param name="source">Source color</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Lighten(Vector4 backdrop, Vector4 source)
        {
            return Vector4.Max(backdrop, source);
        }

        /// <summary>
        /// Overlays source over backdrop
        /// </summary>
        /// <param name="backdrop">Backdrop color</param>
        /// <param name="source">Source color</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Overlay(Vector4 backdrop, Vector4 source)
        {
            float cr = OverlayValueFunction(backdrop.X, source.X);
            float cg = OverlayValueFunction(backdrop.Y, source.Y);
            float cb = OverlayValueFunction(backdrop.Z, source.Z);

            return Vector4.Min(Vector4.One, new Vector4(cr, cg, cb, 0));
        }

        /// <summary>
        /// Hard light effect
        /// </summary>
        /// <param name="backdrop">Backdrop color</param>
        /// <param name="source">Source color</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 HardLight(Vector4 backdrop, Vector4 source)
        {
            float cr = OverlayValueFunction(source.X, backdrop.X);
            float cg = OverlayValueFunction(source.Y, backdrop.Y);
            float cb = OverlayValueFunction(source.Z, backdrop.Z);

            return Vector4.Min(Vector4.One, new Vector4(cr, cg, cb, 0));
        }

        /// <summary>
        /// Helper function for Overlay andHardLight modes
        /// </summary>
        /// <param name="backdrop">Backdrop color element</param>
        /// <param name="source">Source color element</param>
        /// <returns>Overlay value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float OverlayValueFunction(float backdrop, float source)
        {
            return backdrop <= 0.5f ? (2 * backdrop * source) : 1 - ((2 * (1 - source)) * (1 - backdrop));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Over(Vector4 dst, Vector4 src, Vector4 blend)
        {
            // calculate weights
            float blendW = dst.W * src.W;
            float dstW = dst.W - blendW;
            float srcW = src.W - blendW;

            // calculate final alpha
            float alpha = dstW + srcW + blendW;

            // calculate final color
            Vector4 color = (dst * dstW) + (src * srcW) + (blend * blendW);

            // unpremultiply
            color /= MathF.Max(alpha, Constants.Epsilon);
            color.W = alpha;

            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Atop(Vector4 dst, Vector4 src, Vector4 blend)
        {
            // calculate weights
            float blendW = dst.W * src.W;
            float dstW = dst.W - blendW;

            // calculate final alpha
            float alpha = dstW + blendW;

            // calculate final color
            Vector4 color = (dst * dstW) + (blend * blendW);

            // unpremultiply
            color /= MathF.Max(alpha, Constants.Epsilon);
            color.W = alpha;

            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 In(Vector4 dst, Vector4 src, Vector4 blend)
        {
            float alpha = dst.W * src.W;

            Vector4 color = src * alpha;                    // premultiply
            color /= MathF.Max(alpha, Constants.Epsilon);   // unpremultiply
            color.W = alpha;

            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Out(Vector4 dst, Vector4 src)
        {
            float alpha = (1 - dst.W) * src.W;

            Vector4 color = src * alpha;                    // premultiply
            color /= MathF.Max(alpha, Constants.Epsilon);   // unpremultiply
            color.W = alpha;

            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Xor(Vector4 dst, Vector4 src)
        {
            float srcW = 1 - dst.W;
            float dstW = 1 - src.W;

            float alpha = (src.W * srcW) + (dst.W * dstW);
            Vector4 color = (src.W * src * srcW) + (dst.W * dst * dstW);

            // unpremultiply
            color /= MathF.Max(alpha, Constants.Epsilon);
            color.W = alpha;

            return color;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 Clear(Vector4 backdrop, Vector4 source)
        {
            return Vector4.Zero;
        }
    }
}