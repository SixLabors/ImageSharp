// <copyright file="ImageSharpMiddlewareOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System.Collections.Generic;

    using ImageSharp.Web.Caching;
    using ImageSharp.Web.Processors;
    using ImageSharp.Web.Services;

    /// <summary>
    /// Configuration options for the ImageSharp middleware.
    /// </summary>
    public class ImageSharpMiddlewareOptions
    {
        /// <summary>
        /// Gets or sets the configuration
        /// </summary>
        public Configuration Configuration { get; set; } = Configuration.Default;

        /// <summary>
        /// Gets or sets the collecion of image services.
        /// </summary>
        public IList<IImageService> Services { get; set; } = new List<IImageService>();

        /// <summary>
        /// Gets or sets the collecion of image processors.
        /// </summary>
        public IList<IImageWebProcessor> Processors { get; set; } = new List<IImageWebProcessor>();

        /// <summary>
        /// Gets or sets the cache.
        /// </summary>
        public IImageCache Cache { get; set; }

        /// <summary>
        /// Gets or sets the number of days to store images in the cache.
        /// </summary>
        public int MaxCacheDays { get; set; } = 365;
    }
}