// <copyright file="PorterDuffFunctions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Collection of Porter Duff alpha blending functions
    /// </summary>
    /// <typeparam name="TPixel">Pixel Format</typeparam>
    /// <remarks>
    /// These functions are designed to be a general solution for all color cases,
    /// that is, they take in account the alpha value of both the backdrop
    /// and source, and there's no need to alpha-premultiply neither the backdrop
    /// nor the source.
    /// Note there are faster functions for when the backdrop color is known
    /// to be opaque
    /// </remarks>
    internal static class PorterDuffFunctions<TPixel>
        where TPixel : IPixel
    {
        /// <summary>
        /// Source over backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel NormalBlendFunction(TPixel backdrop, TPixel source, float opacity)
        {
            Vector4 l = source.ToVector4();
            l.W *= opacity;
            if (l.W == 0)
            {
                return backdrop;
            }

            Vector4 b = backdrop.ToVector4();

            return Compose(b, l, l);
        }

        /// <summary>
        /// Source multiplied by backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel MultiplyFunction(TPixel backdrop, TPixel source, float opacity)
        {
            Vector4 l = source.ToVector4();
            l.W *= opacity;
            if (l.W == 0)
            {
                return backdrop;
            }

            Vector4 b = backdrop.ToVector4();

            return Compose(b, l, b * l);
        }

        /// <summary>
        /// Source added to backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel AddFunction(TPixel backdrop, TPixel source, float opacity)
        {
            Vector4 l = source.ToVector4();
            l.W *= opacity;
            if (l.W == 0)
            {
                return backdrop;
            }

            Vector4 b = backdrop.ToVector4();

            return Compose(b, l, Vector4.Min(Vector4.One, b + l));
        }

        /// <summary>
        /// Source substracted from backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel SubstractFunction(TPixel backdrop, TPixel source, float opacity)
        {
            Vector4 l = source.ToVector4();
            l.W *= opacity;
            if (l.W == 0)
            {
                return backdrop;
            }

            Vector4 b = backdrop.ToVector4();

            return Compose(b, l, Vector4.Max(Vector4.Zero, b - l));
        }

        /// <summary>
        /// Complement of source multiplied by the complement of backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel ScreenFunction(TPixel backdrop, TPixel source, float opacity)
        {
            Vector4 l = source.ToVector4();
            l.W *= opacity;
            if (l.W == 0)
            {
                return backdrop;
            }

            Vector4 b = backdrop.ToVector4();

            return Compose(b, l, Vector4.One - ((Vector4.One - b) * (Vector4.One - l)));
        }

        /// <summary>
        /// Per element, chooses the smallest value of source and backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel DarkenFunction(TPixel backdrop, TPixel source, float opacity)
        {
            Vector4 l = source.ToVector4();
            l.W *= opacity;
            if (l.W == 0)
            {
                return backdrop;
            }

            Vector4 b = backdrop.ToVector4();

            return Compose(b, l, Vector4.Min(b, l));
        }

        /// <summary>
        /// Per element, chooses the largest value of source and backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel LightenFunction(TPixel backdrop, TPixel source, float opacity)
        {
            Vector4 l = source.ToVector4();
            l.W *= opacity;
            if (l.W == 0)
            {
                return backdrop;
            }

            Vector4 b = backdrop.ToVector4();

            return Compose(b, l, Vector4.Max(b, l));
        }

        /// <summary>
        /// Overlays source over backdrop
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel OverlayFunction(TPixel backdrop, TPixel source, float opacity)
        {
            Vector4 l = source.ToVector4();
            l.W *= opacity;
            if (l.W == 0)
            {
                return backdrop;
            }

            Vector4 b = backdrop.ToVector4();

            float cr = OverlayValueFunction(b.X, l.X);
            float cg = OverlayValueFunction(b.Y, l.Y);
            float cb = OverlayValueFunction(b.Z, l.Z);

            return Compose(b, l, Vector4.Min(Vector4.One, new Vector4(cr, cg, cb, 0)));
        }

        /// <summary>
        /// Hard light effect
        /// </summary>
        /// <param name="backdrop">Backgrop color</param>
        /// <param name="source">Source color</param>
        /// <param name="opacity">Opacity applied to Source Alpha</param>
        /// <returns>Output color</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TPixel HardLightFunction(TPixel backdrop, TPixel source, float opacity)
        {
            Vector4 l = source.ToVector4();
            l.W *= opacity;
            if (l.W == 0)
            {
                return backdrop;
            }

            Vector4 b = backdrop.ToVector4();

            float cr = OverlayValueFunction(l.X, b.X);
            float cg = OverlayValueFunction(l.Y, b.Y);
            float cb = OverlayValueFunction(l.Z, b.Z);

            return Compose(b, l, Vector4.Min(Vector4.One, new Vector4(cr, cg, cb, 0)));
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
        private static TPixel Compose(Vector4 backdrop, Vector4 source, Vector4 xform)
        {
            DebugGuard.MustBeGreaterThan(source.W, 0, nameof(source.W));

            // calculate weights
            float xw = backdrop.W * source.W;
            float bw = backdrop.W - xw;
            float sw = source.W - xw;

            // calculate final alpha
            float a = xw + bw + sw;

            // calculate final value
            xform = ((xform * xw) + (backdrop * bw) + (source * sw)) / a;
            xform.W = a;

            TPixel packed = default(TPixel);
            packed.PackFromVector4(xform);

            return packed;
        }
    }
}