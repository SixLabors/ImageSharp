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
        /// Gets the HardLightBlender.
        /// </summary>
        private PixelBlender<TPixel> hardLightBlender = new DefaultHardLightPixelBlender<TPixel>();

        /// <summary>
        /// Gets the OverlayBlender.
        /// </summary>
        private PixelBlender<TPixel> overlayBlender = new DefaultOverlayPixelBlender<TPixel>();

        /// <summary>
        /// Gets the DarkenBlender.
        /// </summary>
        private PixelBlender<TPixel> darkenBlender = new DefaultDarkenPixelBlender<TPixel>();

        /// <summary>
        /// Gets the LightenBlender.
        /// </summary>
        private PixelBlender<TPixel> lightenBlender = new DefaultLightenPixelBlender<TPixel>();

        /// <summary>
        /// Gets the SoftLightBlender.
        /// </summary>
        private PixelBlender<TPixel> softLightBlender = new DefaultSoftLightPixelBlender<TPixel>();

        /// <summary>
        /// Gets the DodgeBlender.
        /// </summary>
        private PixelBlender<TPixel> dodgeBlender = new DefaultDodgePixelBlender<TPixel>();

        /// <summary>
        /// Gets the BurnBlender.
        /// </summary>
        private PixelBlender<TPixel> burnBlender = new DefaultBurnPixelBlender<TPixel>();

        /// <summary>
        /// Gets the DifferenceBlender.
        /// </summary>
        private PixelBlender<TPixel> differenceBlender = new DefaultDifferencePixelBlender<TPixel>();

        /// <summary>
        /// Gets the DifferenceBlender.
        /// </summary>
        private PixelBlender<TPixel> exclusionBlender = new DefaultExclusionPixelBlender<TPixel>();

        /// <summary>
        /// Gets the PremultipliedLerpBlender.
        /// </summary>
        private PixelBlender<TPixel> premultipliedLerpBlender = new DefaultPremultipliedLerpPixelBlender<TPixel>();

        /// <summary>
        /// Find an instance of the pixel blender.
        /// </summary>
        /// <param name="mode">The blending mode to apply</param>
        /// <returns>A <see cref="PixelBlender{TPixel}"/>.</returns>
        internal virtual PixelBlender<TPixel> GetPixelBlender(PixelBlenderMode mode)
        {
            switch (mode)
            {
                case PixelBlenderMode.Normal:
                    return this.normalBlender;
                case PixelBlenderMode.Multiply:
                    return this.multiplyBlender;
                case PixelBlenderMode.Screen:
                    return this.screenBlender;
                case PixelBlenderMode.HardLight:
                    return this.hardLightBlender;
                case PixelBlenderMode.Overlay:
                    return this.overlayBlender;
                case PixelBlenderMode.Darken:
                    return this.darkenBlender;
                case PixelBlenderMode.Lighten:
                    return this.lightenBlender;
                case PixelBlenderMode.SoftLight:
                    return this.softLightBlender;
                case PixelBlenderMode.Dodge:
                    return this.dodgeBlender;
                case PixelBlenderMode.Burn:
                    return this.burnBlender;
                case PixelBlenderMode.Difference:
                    return this.differenceBlender;
                case PixelBlenderMode.Exclusion:
                    return this.exclusionBlender;
                default:
                    return this.premultipliedLerpBlender;
            }
        }
    }
}