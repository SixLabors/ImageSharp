// <copyright file="ConfigurationExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.IO;

    using Formats;

    /// <summary>
    /// Extension methods for the <see cref="Configuration"/> type.
    /// </summary>
    public static partial class ConfigurationExtensions
    {
        /// <summary>
        /// Adds the common formats, PNG, JPEG, GIF and BMP.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <returns>The config</returns>
        public static Configuration AddCommonFormats(this Configuration config)
        {
            return config
                    .AddPngFormat()
                    .AddJpegFormat()
                    .AddGifFormat()
                    .AddBmpFormat();
        }
    }
}
