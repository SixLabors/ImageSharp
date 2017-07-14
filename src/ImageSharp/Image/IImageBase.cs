// <copyright file="IImageBase.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using SixLabors.Primitives;

    /// <summary>
    /// Encapsulates the basic properties and methods required to manipulate images.
    /// </summary>
    public interface IImageBase
    {
        /// <summary>
        /// Gets the width in pixels.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the height in pixels.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the configuration providing initialization code which allows extending the library.
        /// </summary>
        Configuration Configuration { get; }
    }
}