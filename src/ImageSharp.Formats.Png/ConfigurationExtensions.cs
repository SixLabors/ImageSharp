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
        /// Adds the Png format.
        /// </summary>
        /// <param name="config">The Image configurations.</param>
        /// <returns>The Configuration object</returns>
        public static Configuration AddPngFormat(this Configuration config)
        {
            config.AddImageFormat(new PngFormat());
            return config;
        }
    }
}
