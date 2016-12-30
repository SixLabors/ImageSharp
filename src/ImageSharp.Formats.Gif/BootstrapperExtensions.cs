// <copyright file="BootstrapperExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.IO;

    using Formats;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class BootstrapperExtensions
    {
        /// <summary>
        /// Adds the Gif format.
        /// </summary>
        /// <param name="bootstrapper">The bootstrapper.</param>
        /// <returns>The Bootstrapper</returns>
        public static Bootstrapper AddGifFormat(this Bootstrapper bootstrapper)
        {
            bootstrapper.AddImageFormat(new GifFormat());
            return bootstrapper;
        }
    }
}
