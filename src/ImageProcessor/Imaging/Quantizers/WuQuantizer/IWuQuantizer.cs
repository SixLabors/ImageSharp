// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IWuQuantizer.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The WuQuantizer interface.
//   Adapted from <see href="https://github.com/drewnoakes" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    using System.Drawing;

    /// <summary>
    /// The WuQuantizer interface.
    /// Adapted from <see href="https://github.com/drewnoakes" />
    /// </summary>
    public interface IWuQuantizer
    {
        /// <summary>
        /// Quantizes the given image.
        /// </summary>
        /// <param name="image">
        /// The 32 bit per pixel <see cref="Image"/>.
        /// </param>
        /// <param name="alphaThreshold">
        /// The alpha threshold. All colors with an alpha value less than this will be 
        /// considered fully transparent
        /// </param>
        /// <param name="alphaFader">
        /// The alpha fader. Alpha values will be normalized to the nearest multiple of this value.
        /// </param>
        /// <returns>
        /// The quantized <see cref="Image"/>.
        /// </returns>
        Image QuantizeImage(Bitmap image, int alphaThreshold, int alphaFader);
    }
}