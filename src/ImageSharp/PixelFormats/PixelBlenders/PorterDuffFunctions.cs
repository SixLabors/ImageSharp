// <copyright file="PorterDuffFunctions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats.PixelBlenders
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Collection of Porter Duff alpha blending functions applying an the 'Over' composition model.
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
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Normal(Vector4 backdrop, Vector4 source, float opacity)
        {
            source.W *= opacity;
            return Compose(backdrop, source, source);
        }

        /// <summary>
        /// Source multiplied by backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Multiply(Vector4 backdrop, Vector4 source, float opacity)
        {
            source.W *= opacity;
            return Compose(backdrop, source, backdrop * source);
        }

        /// <summary>
        /// Source added to backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Add(Vector4 backdrop, Vector4 source, float opacity)
        {
            source.W *= opacity;
            return Compose(backdrop, source, Vector4.Min(Vector4.One, backdrop + source));
        }

        /// <summary>
        /// Source substracted from backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Substract(Vector4 backdrop, Vector4 source, float opacity)
        {
            source.W *= opacity;
            return Compose(backdrop, source, Vector4.Max(Vector4.Zero, backdrop - source));
        }

        /// <summary>
        /// Complement of source multiplied by the complement of backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Screen(Vector4 backdrop, Vector4 source, float opacity)
        {
            source.W *= opacity;
            return Compose(backdrop, source, Vector4.One - ((Vector4.One - backdrop) * (Vector4.One - source)));
        }

        /// <summary>
        /// Per element, chooses the smallest value of source and backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Darken(Vector4 backdrop, Vector4 source, float opacity)
        {
            source.W *= opacity;
            return Compose(backdrop, source, Vector4.Min(backdrop, source));
        }

        /// <summary>
        /// Per element, chooses the largest value of source and backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Lighten(Vector4 backdrop, Vector4 source, float opacity)
        {
            source.W *= opacity;
            return Compose(backdrop, source, Vector4.Max(backdrop, source));
        }

        /// <summary>
        /// Overlays source over backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Overlay(Vector4 backdrop, Vector4 source, float opacity)
        {
            source.W *= opacity;
            float cr = OverlayValueFunction(backdrop.X, source.X);
            float cg = OverlayValueFunction(backdrop.Y, source.Y);
            float cb = OverlayValueFunction(backdrop.Z, source.Z);

            return Compose(backdrop, source, Vector4.Min(Vector4.One, new Vector4(cr, cg, cb, 0)));
        }

        /// <summary>
        /// Hard light effect
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 HardLight(Vector4 backdrop, Vector4 source, float opacity)
        {
            source.W *= opacity;
            float cr = OverlayValueFunction(source.X, backdrop.X);
            float cg = OverlayValueFunction(source.Y, backdrop.Y);
            float cb = OverlayValueFunction(source.Z, backdrop.Z);

            return Compose(backdrop, source, Vector4.Min(Vector4.One, new Vector4(cr, cg, cb, 0)));
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

        /// <summary>
        /// General composition function for all modes, with a general solution for alpha channel
        /// </summary>
        /// <param name="backdrop">Original backgrop color</param>
        /// <param name="source">Original source color</param>
        /// <param name="xform">Desired transformed color, without taking Alpha channel in account</param>
        /// <returns>The final color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 Compose(Vector4 backdrop, Vector4 source, Vector4 xform)
        {
            // calculate weights
            float xw = backdrop.W * source.W;
            float bw = backdrop.W - xw;
            float sw = source.W - xw;

            // calculate final alpha
            float a = xw + bw + sw;

            // calculate final value
            xform = ((xform * xw) + (backdrop * bw) + (source * sw)) / MathF.Max(a, Constants.Epsilon);
            xform.W = a;

            return xform;
        }
    }
}