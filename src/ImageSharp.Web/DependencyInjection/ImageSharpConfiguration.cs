// <copyright file="ImageSharpConfiguration.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using System.Collections.Generic;

    using ImageSharp.Web.Caching;
    using ImageSharp.Web.Commands;
    using ImageSharp.Web.Middleware;
    using ImageSharp.Web.Processors;
    using ImageSharp.Web.Resolvers;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Provides default configuration settings to be consumeed by the middleware
    /// </summary>
    public class ImageSharpConfiguration : IConfigureOptions<ImageSharpMiddlewareOptions>
    {
        /// <summary>
        /// The hosting environment the application is running in.
        /// </summary>
        private readonly IHostingEnvironment environment;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharpConfiguration"/> class.
        /// </summary>
        /// <param name="environment">The <see cref="IHostingEnvironment"/> used by this middleware</param>
        public ImageSharpConfiguration(IHostingEnvironment environment)
        {
            this.environment = environment;
        }

        /// <inheritdoc/>
        public void Configure(ImageSharpMiddlewareOptions options)
        {
            options.Configuration = Configuration.Default;
            options.Cache = new PhysicalFileSystemCache(this.environment);
            options.MaxCacheDays = 365;
            options.UriParser = new QueryCollectionUriParser();
            options.Services = new List<IImageResolver> { new PhysicalFileSystemResolver(this.environment) };
            options.Processors = new List<IImageWebProcessor> { new ResizeWebProcessor() };
        }
    }
}