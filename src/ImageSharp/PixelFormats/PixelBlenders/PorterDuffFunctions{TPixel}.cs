// <copyright file="PorterDuffFunctions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats.PixelBlenders
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
            return ToPixel(PorterDuffFunctions.NormalBlendFunction(backdrop.ToVector4(), source.ToVector4(), opacity));
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
            return ToPixel(PorterDuffFunctions.MultiplyFunction(backdrop.ToVector4(), source.ToVector4(), opacity));
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
            return ToPixel(PorterDuffFunctions.AddFunction(backdrop.ToVector4(), source.ToVector4(), opacity));
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
            return ToPixel(PorterDuffFunctions.SubstractFunction(backdrop.ToVector4(), source.ToVector4(), opacity));
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
            return ToPixel(PorterDuffFunctions.ScreenFunction(backdrop.ToVector4(), source.ToVector4(), opacity));
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
            return ToPixel(PorterDuffFunctions.DarkenFunction(backdrop.ToVector4(), source.ToVector4(), opacity));
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
            return ToPixel(PorterDuffFunctions.LightenFunction(backdrop.ToVector4(), source.ToVector4(), opacity));
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
            return ToPixel(PorterDuffFunctions.OverlayFunction(backdrop.ToVector4(), source.ToVector4(), opacity));
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
            return ToPixel(PorterDuffFunctions.HardLightFunction(backdrop.ToVector4(), source.ToVector4(), opacity));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TPixel ToPixel(Vector4 vector)
        {
            TPixel p = default(TPixel);
            p.PackFromVector4(vector);
            return p;
        }
    }
}