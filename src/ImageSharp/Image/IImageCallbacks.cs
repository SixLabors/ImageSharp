// <copyright file="IImageCallbacks.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using ImageSharp.Processing;

    /// <summary>
    /// Provides a set of methods that are called as part of the images lifetime/processes.
    /// </summary>
    internal interface IImageCallbacks
    {
        /// <summary>
        /// Invoked before the image is saved.
        /// </summary>
        /// <typeparam name="TColor">The color</typeparam>
        /// <param name="image">The image</param>
        /// <param name="stream">The destination stream</param>
        /// <param name="encoder">The encoder</param>
        /// <param name="options">The options</param>
        /// <returns>
        /// return true if the processor should be applied otherwise false.
        /// </returns>
        bool OnSaving<TColor>(ImageBase<TColor> image, Stream stream, Formats.IImageEncoder encoder, IEncoderOptions options)
            where TColor : struct, IPixel<TColor>;

        /// <summary>
        /// Invoked before the image is processed.
        /// </summary>
        /// <typeparam name="TColor">The color</typeparam>
        /// <param name="image">The image</param>
        /// <param name="processor">The processor.</param>
        /// <param name="rectangle">The rectangle.</param>
        /// <returns>
        /// return true if the processor should be applied otherwise false.
        /// </returns>
        bool OnProcessing<TColor>(ImageBase<TColor> image, IImageProcessor<TColor> processor, Rectangle rectangle)
            where TColor : struct, IPixel<TColor>;
    }
}
