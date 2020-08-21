// Copyright (c) Six Labors.
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
        /// Returns the result of the "Normal" compositing equation.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Normal(Vector4 backdrop, Vector4 source)
        {
            return source;
        }

        /// <summary>
        /// Returns the result of the "Multiply" compositing equation.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Multiply(Vector4 backdrop, Vector4 source)
        {
            return backdrop * source;
        }

        /// <summary>
        /// Returns the result of the "Add" compositing equation.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Add(Vector4 backdrop, Vector4 source)
        {
            return Vector4.Min(Vector4.One, backdrop + source);
        }

        /// <summary>
        /// Returns the result of the "Subtract" compositing equation.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Subtract(Vector4 backdrop, Vector4 source)
        {
            return Vector4.Max(Vector4.Zero, backdrop - source);
        }

        /// <summary>
        /// Returns the result of the "Screen" compositing equation.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Screen(Vector4 backdrop, Vector4 source)
        {
            return Vector4.One - ((Vector4.One - backdrop) * (Vector4.One - source));
        }

        /// <summary>
        /// Returns the result of the "Darken" compositing equation.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Darken(Vector4 backdrop, Vector4 source)
        {
            return Vector4.Min(backdrop, source);
        }

        /// <summary>
        /// Returns the result of the "Lighten" compositing equation.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Lighten(Vector4 backdrop, Vector4 source)
        {
            return Vector4.Max(backdrop, source);
        }

        /// <summary>
        /// Returns the result of the "Overlay" compositing equation.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Overlay(Vector4 backdrop, Vector4 source)
        {
            float cr = OverlayValueFunction(backdrop.X, source.X);
            float cg = OverlayValueFunction(backdrop.Y, source.Y);
            float cb = OverlayValueFunction(backdrop.Z, source.Z);

            return Vector4.Min(Vector4.One, new Vector4(cr, cg, cb, 0));
        }

        /// <summary>
        /// Returns the result of the "HardLight" compositing equation.
        /// </summary>
        /// <param name="backdrop">The backdrop vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
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
            return backdrop <= 0.5f ? (2 * backdrop * source) : 1 - (2 * (1 - source) * (1 - backdrop));
        }

        /// <summary>
        /// Returns the result of the "Over" compositing equation.
        /// </summary>
        /// <param name="destination">The destination vector.</param>
        /// <param name="source">The source vector.</param>
        /// <param name="blend">The amount to blend. Range 0..1</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Over(Vector4 destination, Vector4 source, Vector4 blend)
        {
            // calculate weights
            float blendW = destination.W * source.W;
            float dstW = destination.W - blendW;
            float srcW = source.W - blendW;

            // calculate final alpha
            float alpha = dstW + source.W;

            // calculate final color
            Vector4 color = (destination * dstW) + (source * srcW) + (blend * blendW);

            // unpremultiply
            color /= MathF.Max(alpha, Constants.Epsilon);
            color.W = alpha;

            return color;
        }

        /// <summary>
        /// Returns the result of the "Atop" compositing equation.
        /// </summary>
        /// <param name="destination">The destination vector.</param>
        /// <param name="source">The source vector.</param>
        /// <param name="blend">The amount to blend. Range 0..1</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Atop(Vector4 destination, Vector4 source, Vector4 blend)
        {
            // calculate weights
            float blendW = destination.W * source.W;
            float dstW = destination.W - blendW;

            // calculate final alpha
            float alpha = destination.W;

            // calculate final color
            Vector4 color = (destination * dstW) + (blend * blendW);

            // unpremultiply
            color /= MathF.Max(alpha, Constants.Epsilon);
            color.W = alpha;

            return color;
        }

        /// <summary>
        /// Returns the result of the "In" compositing equation.
        /// </summary>
        /// <param name="destination">The destination vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 In(Vector4 destination, Vector4 source)
        {
            float alpha = destination.W * source.W;

            Vector4 color = source * alpha;                    // premultiply
            color /= MathF.Max(alpha, Constants.Epsilon);   // unpremultiply
            color.W = alpha;

            return color;
        }

        /// <summary>
        /// Returns the result of the "Out" compositing equation.
        /// </summary>
        /// <param name="destination">The destination vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Out(Vector4 destination, Vector4 source)
        {
            float alpha = (1 - destination.W) * source.W;

            Vector4 color = source * alpha;                    // premultiply
            color /= MathF.Max(alpha, Constants.Epsilon);   // unpremultiply
            color.W = alpha;

            return color;
        }

        /// <summary>
        /// Returns the result of the "XOr" compositing equation.
        /// </summary>
        /// <param name="destination">The destination vector.</param>
        /// <param name="source">The source vector.</param>
        /// <returns>The <see cref="Vector4"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Xor(Vector4 destination, Vector4 source)
        {
            float srcW = 1 - destination.W;
            float dstW = 1 - source.W;

            float alpha = (source.W * srcW) + (destination.W * dstW);
            Vector4 color = (source.W * source * srcW) + (destination.W * destination * dstW);

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
