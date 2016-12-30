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
        /// Adds the common formats, PNG, JPEG, GIF and BMP.
        /// </summary>
        /// <param name="bootstrapper">The bootstrapper.</param>
        /// <returns>The bootstrapper</returns>
        public static Bootstrapper AddCommonFormats(this Bootstrapper bootstrapper)
        {
            return bootstrapper
                    .AddPngFormat()
                    .AddJpegFormat()
                    .AddGifFormat()
                    .AddBmpFormat();
        }
    }
}
