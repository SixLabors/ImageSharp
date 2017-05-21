// <copyright file="ImageSharpBuilder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Allows fine grained configuration of ImageSharp services.
    /// </summary>
    internal class ImageSharpBuilder : IImageSharpBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharpBuilder"/> class.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        public ImageSharpBuilder(IServiceCollection services)
        {
            this.Services = services;
        }

        /// <inheritdoc/>
        public IServiceCollection Services { get; }
    }
}