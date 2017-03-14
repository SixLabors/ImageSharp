// <copyright file="IGifDecoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Text;

    /// <summary>
    /// Encapsulates the options for the <see cref="GifDecoder"/>.
    /// </summary>
    public interface IGifDecoderOptions : IDecoderOptions
    {
        /// <summary>
        /// Gets the encoding that should be used when reading comments.
        /// </summary>
        Encoding TextEncoding { get; }
    }
}
