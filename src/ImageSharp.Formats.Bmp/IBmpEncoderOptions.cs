// <copyright file="IBmpEncoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Encapsulates the options for the <see cref="BmpEncoder"/>.
    /// </summary>
    public interface IBmpEncoderOptions : IEncoderOptions
    {
        /// <summary>
        /// Gets the number of bits per pixel.
        /// </summary>
        BmpBitsPerPixel BitsPerPixel { get; }
    }
}
