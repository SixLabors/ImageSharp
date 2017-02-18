// <copyright file="IPngDecoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Text;

    /// <summary>
    /// Encapsulates the png decoder options.
    /// </summary>
    public interface IPngDecoderOptions : IDecoderOptions
    {
        /// <summary>
        /// Gets the encoding that should be used when reading text chunks.
        /// </summary>
        Encoding TextEncoding { get; }
    }
}
