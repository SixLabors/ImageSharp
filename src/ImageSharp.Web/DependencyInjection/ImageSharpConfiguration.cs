// <copyright file="ImageSharpConfiguration.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using ImageSharp.Web.Middleware;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Provides default configuration settings to be consumeed by the middleware
    /// </summary>
    public class ImageSharpConfiguration : IConfigureOptions<ImageSharpMiddlewareOptions>
    {
        /// <inheritdoc/>
        public void Configure(ImageSharpMiddlewareOptions options)
        {
            options.Configuration = Configuration.Default;
            options.MaxCacheDays = 365;
            options.MaxBrowserCacheDays = 7;
        }
    }
}