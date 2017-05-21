// <copyright file="ImageSharpCoreBuilder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Allows fine grained configuration of ImageSharp services.
    /// </summary>
    internal class ImageSharpCoreBuilder : IImageSharpCoreBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharpCoreBuilder"/> class.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        public ImageSharpCoreBuilder(IServiceCollection services)
        {
            this.Services = services;
        }

        /// <inheritdoc/>
        public IServiceCollection Services { get; }
    }
}