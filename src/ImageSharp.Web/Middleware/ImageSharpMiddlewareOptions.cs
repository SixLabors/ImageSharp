// <copyright file="ImageSharpMiddlewareOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Middleware
{
    using System.Collections.Generic;

    using ImageSharp.Web.Services;

    /// <summary>
    /// Configuration options for the ImageSharp middleware.
    /// </summary>
    public class ImageSharpMiddlewareOptions
    {
        /// <summary>
        /// Gets or sets the collecion of image services.
        /// </summary>
        public IList<IImageService> Services { get; set; } = new List<IImageService>();
    }
}