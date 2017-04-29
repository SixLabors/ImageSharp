// <copyright file="PixelOperations{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using ImageSharp.PixelFormats.PixelBlenders;

#pragma warning disable CS1710 // XML comment has a duplicate typeparam tag
    /// <summary>
    /// A stateless class implementing Strategy Pattern for batched pixel-data conversion operations
    /// for pixel buffers of type <typeparamref name="TPixel"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public partial class PixelOperations<TPixel>
#pragma warning restore CS1710 // XML comment has a duplicate typeparam tag
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the NormalBlender.
        /// </summary>
        private PixelBlender<TPixel> normalBlender = new DefaultNormalPixelBlender<TPixel>();

        /// <summary>
        /// Gets the MultiplyBlender.
        /// </summary>
        private PixelBlender<TPixel> multiplyBlender = new DefaultMultiplyPixelBlender<TPixel>();

        /// <summary>
        /// Gets the ScreenBlender.
        /// </summary>
        private PixelBlender<TPixel> screenBlender = new DefaultScreenPixelBlender<TPixel>();

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