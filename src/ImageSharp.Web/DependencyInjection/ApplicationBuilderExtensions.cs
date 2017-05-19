// <copyright file="ApplicationBuilderExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.DependencyInjection
{
    using ImageSharp.Web.Middleware;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/> to simplify middleware registration.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Registers the ImageSharp middleware with the default options
        /// </summary>
        /// <param name="app">The application with the mechanism to configure a request pipeline</param>
        /// <returns><see cref="IApplicationBuilder"/></returns>
        public static IApplicationBuilder UseImageSharp(this IApplicationBuilder app)
        {
            Guard.NotNull(app, nameof(app));

            return app.UseMiddleware<ImageSharpMiddleware>(Options.Create(new ImageSharpMiddlewareOptions()));
        }

        /// <summary>
        /// Registers the ImageSharp middleware with the given options
        /// </summary>
        /// <param name="app">The application with the mechanism to configure a request pipeline</param>
        /// <param name="options">The middleware options.</param>
        /// <returns><see cref="IApplicationBuilder"/></returns>
        public static IApplicationBuilder UseImageSharp(this IApplicationBuilder app, ImageSharpMiddlewareOptions options)
        {
            Guard.NotNull(app, nameof(app));
            Guard.NotNull(options, nameof(options));

            return app.UseMiddleware<ImageSharpMiddleware>(Options.Create(options));
        }
    }
}