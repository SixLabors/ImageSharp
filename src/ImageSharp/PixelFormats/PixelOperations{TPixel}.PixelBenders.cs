// <copyright file="PixelOperations{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using ImageSharp.PixelFormats.PixelBlenders;

    /// <content>
    /// Provides access to pixel blenders
    /// </content>
    public partial class PixelOperations<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Find an instance of the pixel blender.
        /// </summary>
        /// <param name="mode">The blending mode to apply</param>
        /// <returns>A <see cref="PixelBlender{TPixel}"/>.</returns>
        internal virtual PixelBlender<TPixel> GetPixelBlender(PixelBlenderMode mode)
        {
            switch (mode)
            {
                case PixelBlenderMode.Multiply:
                    return DefaultMultiplyPixelBlender<TPixel>.Instance;
                case PixelBlenderMode.Add:
                    return DefaultAddPixelBlender<TPixel>.Instance;
                case PixelBlenderMode.Substract:
                    return DefaultSubstractPixelBlender<TPixel>.Instance;
                case PixelBlenderMode.Screen:
                    return DefaultScreenPixelBlender<TPixel>.Instance;
                case PixelBlenderMode.Darken:
                    return DefaultDarkenPixelBlender<TPixel>.Instance;
                case PixelBlenderMode.Lighten:
                    return DefaultLightenPixelBlender<TPixel>.Instance;
                case PixelBlenderMode.Overlay:
                    return DefaultOverlayPixelBlender<TPixel>.Instance;
                case PixelBlenderMode.HardLight:
                    return DefaultHardLightPixelBlender<TPixel>.Instance;
                case PixelBlenderMode.Normal:
                default:
                    return DefaultNormalPixelBlender<TPixel>.Instance;
            }
        }
    }
}