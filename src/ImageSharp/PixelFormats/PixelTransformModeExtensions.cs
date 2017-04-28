// <copyright file="IPixel.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using PixelFormats;

    /// <summary>
    /// Extensions to retrieve the appropiate pixel transformation functions for <see cref="PixelTransformMode"/>
    /// </summary>
    public static class PixelTransformModeExtensions
    {
        /// <summary>
        /// Gets a pixel transformation function
        /// </summary>
        /// <typeparam name="TPixel">The pixel format used for both Backdrop and Source</typeparam>
        /// <param name="mode">The Duff Porter mode</param>
        /// <returns>A function that transforms a Backdrop and Source colors into a final color</returns>
        public static Func<TPixel, TPixel, float, TPixel> GetPixelFunction<TPixel>(this PixelTransformMode mode)
            where TPixel : IPixel
        {
            return mode.GetPixelFunction<TPixel, TPixel>();
        }

        /// <summary>
        /// Gets a pixel transformation function
        /// </summary>
        /// <typeparam name="TBckPixel">The pixel format used for Backdrop and Output</typeparam>
        /// <typeparam name="TSrcPixel">The pixel format used for Source</typeparam>
        /// <param name="mode">The Duff Porter mode</param>
        /// <returns>A function that transforms a Backdrop and Source colors into a final color</returns>
        public static Func<TBckPixel, TSrcPixel, float, TBckPixel> GetPixelFunction<TBckPixel, TSrcPixel>(this PixelTransformMode mode)
            where TBckPixel : IPixel
            where TSrcPixel : IPixel
        {
            switch (mode)
            {
                case PixelTransformMode.Normal: return PorterDuffFunctions<TBckPixel, TSrcPixel>.NormalBlendFunction;
                case PixelTransformMode.Multiply: return PorterDuffFunctions<TBckPixel, TSrcPixel>.MultiplyFunction;
                case PixelTransformMode.Add: return PorterDuffFunctions<TBckPixel, TSrcPixel>.AddFunction;
                case PixelTransformMode.Screen: return PorterDuffFunctions<TBckPixel, TSrcPixel>.ScreenFunction;
                case PixelTransformMode.Darken: return PorterDuffFunctions<TBckPixel, TSrcPixel>.DarkenFunction;
                case PixelTransformMode.Lighten: return PorterDuffFunctions<TBckPixel, TSrcPixel>.LightenFunction;
                case PixelTransformMode.Overlay: return PorterDuffFunctions<TBckPixel, TSrcPixel>.OverlayFunction;
                case PixelTransformMode.HardLight: return PorterDuffFunctions<TBckPixel, TSrcPixel>.HardLightFunction;

                default: throw new NotImplementedException(nameof(mode));
            }
        }
    }
}
