// <copyright file="IDecoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Encapsulates the shared decoder options.
    /// </summary>
    public interface IDecoderOptions
    {
        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        bool IgnoreMetadata { get; }
    }
}
